#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
SOURCE_SCRIPT="$(cd "${SCRIPT_DIR}/.." && pwd)/wildlife-deploy"
readonly SOURCE_SCRIPT
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/wildlife-backup-restore-test.XXXXXX")"
readonly TEST_ROOT
BACKUP_FILE="${TEST_ROOT}/database.dump"
readonly BACKUP_FILE
CORRUPTED_BACKUP_FILE="${TEST_ROOT}/database-corrupted.dump"
readonly CORRUPTED_BACKUP_FILE
DATABASE_IMAGE="postgres:18.4-alpine3.24@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15"
readonly DATABASE_IMAGE
SOURCE_CONTAINER="wildlife-backup-restore-test-${RANDOM}-$$"
readonly SOURCE_CONTAINER

cleanup() {
    docker rm --force "${SOURCE_CONTAINER}" > /dev/null 2>&1 || true
    if [[ -d "${TEST_ROOT}" \
        && "$(basename -- "${TEST_ROOT}")" == wildlife-backup-restore-test.* ]]; then
        rm -rf -- "${TEST_ROOT}"
    fi
}
trap cleanup EXIT

docker run --detach --rm \
    --name "${SOURCE_CONTAINER}" \
    --network none \
    --env POSTGRES_PASSWORD=restore-test-only \
    --env POSTGRES_DB=wildlife_restore_source \
    "${DATABASE_IMAGE}" > /dev/null

database_ready=false
for _ in {1..30}; do
    if docker exec "${SOURCE_CONTAINER}" \
        pg_isready --username=postgres --dbname=wildlife_restore_source > /dev/null 2>&1; then
        database_ready=true
        break
    fi
    sleep 1
done
[[ "${database_ready}" == true ]] \
    || { printf 'FAIL: source PostgreSQL did not become ready.\n' >&2; exit 1; }

docker exec "${SOURCE_CONTAINER}" \
    psql \
        --username=postgres \
        --dbname=wildlife_restore_source \
        --set=ON_ERROR_STOP=1 \
        --command='CREATE TABLE restore_probe (id integer PRIMARY KEY, value text NOT NULL); INSERT INTO restore_probe VALUES (1, '\''verified'\'');' \
        > /dev/null
docker exec "${SOURCE_CONTAINER}" \
    pg_dump \
        --format=custom \
        --username=postgres \
        --dbname=wildlife_restore_source \
        > "${BACKUP_FILE}"

# shellcheck source=/dev/null
source "${SOURCE_SCRIPT}"

compose_with() {
    local _manifest="$1"
    shift

    [[ "$*" == "ps -q db" ]] || return 1
    printf '%s\n' "${SOURCE_CONTAINER}"
}

verify_database_restore unused-manifest "${BACKUP_FILE}" > /dev/null

head -c 64 "${BACKUP_FILE}" > "${CORRUPTED_BACKUP_FILE}"
if verify_database_restore unused-manifest "${CORRUPTED_BACKUP_FILE}" > /dev/null 2>&1; then
    printf 'FAIL: corrupted database backup passed restore verification.\n' >&2
    exit 1
fi

printf 'PASS: database backup restore verification tests completed.\n'
