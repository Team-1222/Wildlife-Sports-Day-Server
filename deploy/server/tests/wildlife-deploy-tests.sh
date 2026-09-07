#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
SOURCE_SCRIPT="$(cd "${SCRIPT_DIR}/.." && pwd)/wildlife-deploy"
readonly SOURCE_SCRIPT
TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/wildlife-deploy-test.XXXXXX")"
readonly TEST_ROOT
TEST_SCRIPT="${TEST_ROOT}/wildlife-deploy"
readonly TEST_SCRIPT
CALL_LOG="${TEST_ROOT}/compose-calls.log"
readonly CALL_LOG
VERIFY_COUNT_FILE="${TEST_ROOT}/verify-count"
readonly VERIFY_COUNT_FILE
BACKUP_MIRROR_DIR="${TEST_ROOT}/mnt/d/WildlifeBackups"
readonly BACKUP_MIRROR_DIR

cleanup() {
    if [[ -d "${TEST_ROOT}" && "$(basename -- "${TEST_ROOT}")" == wildlife-deploy-test.* ]]; then
        rm -rf -- "${TEST_ROOT}"
    fi
}
trap cleanup EXIT

sed \
    -e "s|/opt/wildlife|${TEST_ROOT}/opt/wildlife|g" \
    -e "s|/var/lib/wildlife|${TEST_ROOT}/var/lib/wildlife|g" \
    -e "s|/var/backups/wildlife|${TEST_ROOT}/var/backups/wildlife|g" \
    -e "s|/run/lock|${TEST_ROOT}/run/lock|g" \
    -e "s|/etc/wildlife|${TEST_ROOT}/etc/wildlife|g" \
    -e "s|/mnt/|${TEST_ROOT}/mnt/|g" \
    "${SOURCE_SCRIPT}" > "${TEST_SCRIPT}"

# shellcheck source=/dev/null
source "${TEST_SCRIPT}"

mkdir -p \
    "${STATE_DIR}" \
    "$(dirname -- "${REQUEST_FILE}")" \
    "${BACKUP_DIR}" \
    "${BACKUP_MIRROR_DIR}" \
    "$(dirname -- "${COMPOSE_FILE}")" \
    "${TEST_ROOT}/etc/wildlife" \
    "${TEST_ROOT}/run/lock"

readonly CURRENT_RELEASE="1111111111111111111111111111111111111111"
readonly PREVIOUS_RELEASE="2222222222222222222222222222222222222222"
readonly CANDIDATE_RELEASE="3333333333333333333333333333333333333333"
readonly CURRENT_APP_DIGEST="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
readonly PREVIOUS_APP_DIGEST="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
readonly CANDIDATE_APP_DIGEST="cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
readonly CURRENT_MIGRATOR_DIGEST="dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
readonly PREVIOUS_MIGRATOR_DIGEST="eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee"
readonly CANDIDATE_MIGRATOR_DIGEST="ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff"

compose_mode="success"
verify_mode="success"
mirror_device_mode="separate"

{
    printf 'PUBLIC_HOST=example.invalid\n'
    printf 'BACKUP_MIRROR_DIR=%s\n' "${BACKUP_MIRROR_DIR}"
} > "${TEST_ROOT}/etc/wildlife/deploy.env"
chmod 600 "${TEST_ROOT}/etc/wildlife/deploy.env"

write_test_manifest() {
    local path="$1"
    local release="$2"
    local app_digest="$3"
    local migrator_digest="$4"

    {
        printf 'RELEASE_ID=%s\n' "${release}"
        printf 'APP_IMAGE=ghcr.io/team-1222/wildlife-sports-day-server@sha256:%s\n' "${app_digest}"
        printf 'MIGRATOR_IMAGE=ghcr.io/team-1222/wildlife-sports-day-server-migrator@sha256:%s\n' "${migrator_digest}"
    } > "${path}"
    chmod 600 "${path}"
}

reset_state() {
    rm -f -- \
        "${CURRENT_FILE}" \
        "${PREVIOUS_FILE}" \
        "${REQUEST_FILE}" \
        "${STATE_DIR}/candidate.env" \
        "${CALL_LOG}" \
        "${VERIFY_COUNT_FILE}"
    find "${BACKUP_DIR}" "${BACKUP_MIRROR_DIR}" \
        -maxdepth 1 -type f -delete

    write_test_manifest \
        "${CURRENT_FILE}" "${CURRENT_RELEASE}" "${CURRENT_APP_DIGEST}" "${CURRENT_MIGRATOR_DIGEST}"
    write_test_manifest \
        "${PREVIOUS_FILE}" "${PREVIOUS_RELEASE}" "${PREVIOUS_APP_DIGEST}" "${PREVIOUS_MIGRATOR_DIGEST}"
    write_test_manifest \
        "${REQUEST_FILE}" "${CANDIDATE_RELEASE}" "${CANDIDATE_APP_DIGEST}" "${CANDIDATE_MIGRATOR_DIGEST}"
    printf '0\n' > "${VERIFY_COUNT_FILE}"
    compose_mode="success"
    verify_mode="success"
}

stat() {
    if [[ "${1:-}" == "-c" && "${2:-}" == "%U" && "${4:-}" == "${REQUEST_FILE}" ]]; then
        printf 'deploy\n'
        return 0
    fi
    if [[ "${1:-}" == "-c" && "${2:-}" == "%U" \
        && ( "${4:-}" == "${BACKUP_MIRROR_DIR}" \
            || "${4:-}" == "${BACKUP_MIRROR_DIR}/"* ) ]]; then
        printf 'root\n'
        return 0
    fi
    if [[ "${1:-}" == "-c" && "${2:-}" == "%a" && "${4:-}" == "${BACKUP_MIRROR_DIR}" ]]; then
        printf '700\n'
        return 0
    fi
    if [[ "${1:-}" == "-c" && "${2:-}" == "%d" && "${4:-}" == "${BACKUP_DIR}" ]]; then
        printf '100\n'
        return 0
    fi
    if [[ "${1:-}" == "-c" && "${2:-}" == "%d" && "${4:-}" == "${BACKUP_MIRROR_DIR}" ]]; then
        if [[ "${mirror_device_mode}" == "same" ]]; then
            printf '100\n'
        else
            printf '200\n'
        fi
        return 0
    fi

    command stat "$@"
}

read_public_host() {
    printf 'example.invalid'
}

wait_for_database() {
    return 0
}

create_database_backup() {
    return 0
}

compose_with() {
    local manifest="$1"
    shift
    local manifest_name
    local command_line="$*"

    manifest_name="$(basename -- "${manifest}")"
    printf '%s|%s\n' "${manifest_name}" "${command_line}" >> "${CALL_LOG}"

    if [[ "${compose_mode}" == "candidate-start-failure" \
        && "${manifest_name}" == "candidate.env" \
        && "${command_line}" == "up -d --no-deps app caddy" ]]; then
        return 1
    fi

    if [[ "${compose_mode}" == "rollback-start-failure" \
        && "${manifest_name}" == .rollback.* \
        && "${command_line}" == "up -d --no-deps app caddy" ]]; then
        return 1
    fi

    return 0
}

verify_application() {
    local verification_count

    verification_count="$(< "${VERIFY_COUNT_FILE}")"
    printf '%s\n' "$((verification_count + 1))" > "${VERIFY_COUNT_FILE}"

    if [[ "${verify_mode}" == "first-failure" && "${verification_count}" -eq 0 ]]; then
        return 1
    fi

    return 0
}

assert_file_equals() {
    local expected="$1"
    local actual="$2"
    local message="$3"

    if ! cmp --silent "${expected}" "${actual}"; then
        printf 'FAIL: %s\n' "${message}" >&2
        exit 1
    fi
}

assert_log_contains() {
    local expected="$1"

    if ! grep -Fqx -- "${expected}" "${CALL_LOG}"; then
        printf 'FAIL: missing compose call: %s\n' "${expected}" >&2
        exit 1
    fi
}

test_backup_mirror_configuration_requires_separate_filesystem() {
    local resolved_path

    mirror_device_mode="separate"
    resolved_path="$(read_backup_mirror_dir)"
    [[ "${resolved_path}" == "${BACKUP_MIRROR_DIR}" ]] \
        || { printf 'FAIL: backup mirror path was not resolved.\n' >&2; exit 1; }

    mirror_device_mode="same"
    if (read_backup_mirror_dir > /dev/null 2>&1); then
        printf 'FAIL: backup mirror on the WSL filesystem was accepted.\n' >&2
        exit 1
    fi
    mirror_device_mode="separate"
}

test_backup_mirror_copies_exact_file() {
    local backup
    local mirrored_backup

    reset_state
    backup="${BACKUP_DIR}/wildlife-20260906T000000Z-${CURRENT_RELEASE}.dump"
    mirrored_backup="${BACKUP_MIRROR_DIR}/$(basename -- "${backup}")"
    printf 'verified backup contents\n' > "${backup}"
    chmod 600 "${backup}"

    mirror_database_backup "${backup}" "${BACKUP_MIRROR_DIR}" > /dev/null

    assert_file_equals "${backup}" "${mirrored_backup}" \
        "Windows backup mirror does not match the local backup."
    [[ "$(command stat -c '%a' -- "${mirrored_backup}")" == "600" ]] \
        || { printf 'FAIL: mirrored backup does not use mode 600.\n' >&2; exit 1; }
}

test_backup_retention_keeps_seven_copies_per_directory() {
    local backup_index
    local backup_name
    local local_count
    local mirror_count

    reset_state
    for backup_index in {1..9}; do
        backup_name="wildlife-20260906T00000${backup_index}Z-${CURRENT_RELEASE}.dump"
        printf '%s\n' "${backup_index}" > "${BACKUP_DIR}/${backup_name}"
        printf '%s\n' "${backup_index}" > "${BACKUP_MIRROR_DIR}/${backup_name}"
        touch -d "@${backup_index}" \
            "${BACKUP_DIR}/${backup_name}" \
            "${BACKUP_MIRROR_DIR}/${backup_name}"
    done

    prune_database_backups "${BACKUP_MIRROR_DIR}"

    local_count="$(find "${BACKUP_DIR}" -maxdepth 1 -type f -name 'wildlife-*.dump' | wc -l)"
    mirror_count="$(find "${BACKUP_MIRROR_DIR}" -maxdepth 1 -type f -name 'wildlife-*.dump' | wc -l)"
    [[ "${local_count//[[:space:]]/}" == "7" ]] \
        || { printf 'FAIL: local backup retention did not keep seven files.\n' >&2; exit 1; }
    [[ "${mirror_count//[[:space:]]/}" == "7" ]] \
        || { printf 'FAIL: mirror backup retention did not keep seven files.\n' >&2; exit 1; }
}

test_apply_start_failure_restores_current_release() {
    local expected_current="${TEST_ROOT}/expected-current.env"
    local expected_previous="${TEST_ROOT}/expected-previous.env"

    reset_state
    cp -- "${CURRENT_FILE}" "${expected_current}"
    cp -- "${PREVIOUS_FILE}" "${expected_previous}"
    compose_mode="candidate-start-failure"

    if (apply_release > /dev/null 2>&1); then
        printf 'FAIL: candidate startup failure returned success.\n' >&2
        exit 1
    fi

    assert_file_equals "${expected_current}" "${CURRENT_FILE}" \
        "Candidate startup failure changed the current manifest."
    assert_file_equals "${expected_previous}" "${PREVIOUS_FILE}" \
        "Candidate startup failure changed the previous manifest."
    [[ ! -e "${STATE_DIR}/candidate.env" ]] \
        || { printf 'FAIL: candidate manifest was not removed.\n' >&2; exit 1; }
    assert_log_contains "candidate.env|up -d --no-deps app caddy"
    assert_log_contains "current.env|up -d --no-deps app caddy"
}

test_apply_health_failure_restores_current_release() {
    local expected_current="${TEST_ROOT}/expected-current.env"
    local expected_previous="${TEST_ROOT}/expected-previous.env"

    reset_state
    cp -- "${CURRENT_FILE}" "${expected_current}"
    cp -- "${PREVIOUS_FILE}" "${expected_previous}"
    verify_mode="first-failure"

    if (apply_release > /dev/null 2>&1); then
        printf 'FAIL: candidate health failure returned success.\n' >&2
        exit 1
    fi

    assert_file_equals "${expected_current}" "${CURRENT_FILE}" \
        "Candidate health failure changed the current manifest."
    assert_file_equals "${expected_previous}" "${PREVIOUS_FILE}" \
        "Candidate health failure changed the previous manifest."
    assert_log_contains "current.env|up -d --no-deps app caddy"
}

test_apply_success_promotes_candidate_after_verification() {
    local expected_current="${TEST_ROOT}/expected-current.env"
    local expected_candidate="${TEST_ROOT}/expected-candidate.env"

    reset_state
    cp -- "${CURRENT_FILE}" "${expected_current}"
    cp -- "${REQUEST_FILE}" "${expected_candidate}"

    apply_release > /dev/null

    assert_file_equals "${expected_candidate}" "${CURRENT_FILE}" \
        "Successful deployment did not promote the candidate manifest."
    assert_file_equals "${expected_current}" "${PREVIOUS_FILE}" \
        "Successful deployment did not retain the prior current manifest."
}

test_manual_rollback_start_failure_preserves_release_state() {
    local expected_current="${TEST_ROOT}/expected-current.env"
    local expected_previous="${TEST_ROOT}/expected-previous.env"

    reset_state
    cp -- "${CURRENT_FILE}" "${expected_current}"
    cp -- "${PREVIOUS_FILE}" "${expected_previous}"
    compose_mode="rollback-start-failure"

    if (rollback_release > /dev/null 2>&1); then
        printf 'FAIL: rollback startup failure returned success.\n' >&2
        exit 1
    fi

    assert_file_equals "${expected_current}" "${CURRENT_FILE}" \
        "Rollback startup failure changed the current manifest."
    assert_file_equals "${expected_previous}" "${PREVIOUS_FILE}" \
        "Rollback startup failure changed the previous manifest."
    assert_log_contains "current.env|up -d --no-deps app caddy"
}

test_manual_rollback_success_swaps_release_state_after_verification() {
    local expected_current="${TEST_ROOT}/expected-current.env"
    local expected_previous="${TEST_ROOT}/expected-previous.env"

    reset_state
    cp -- "${CURRENT_FILE}" "${expected_previous}"
    cp -- "${PREVIOUS_FILE}" "${expected_current}"

    rollback_release > /dev/null

    assert_file_equals "${expected_current}" "${CURRENT_FILE}" \
        "Successful rollback did not promote the rollback target."
    assert_file_equals "${expected_previous}" "${PREVIOUS_FILE}" \
        "Successful rollback did not retain the original current manifest."
}

test_backup_mirror_configuration_requires_separate_filesystem
test_backup_mirror_copies_exact_file
test_backup_retention_keeps_seven_copies_per_directory
test_apply_start_failure_restores_current_release
test_apply_health_failure_restores_current_release
test_apply_success_promotes_candidate_after_verification
test_manual_rollback_start_failure_preserves_release_state
test_manual_rollback_success_swaps_release_state_after_verification

printf 'PASS: wildlife deployment tests completed.\n'
