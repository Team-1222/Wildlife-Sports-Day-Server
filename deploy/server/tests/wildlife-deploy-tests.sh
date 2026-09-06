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
    "${SOURCE_SCRIPT}" > "${TEST_SCRIPT}"

# shellcheck source=/dev/null
source "${TEST_SCRIPT}"

mkdir -p \
    "${STATE_DIR}" \
    "$(dirname -- "${REQUEST_FILE}")" \
    "${BACKUP_DIR}" \
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

prune_database_backups() {
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

test_apply_start_failure_restores_current_release
test_apply_health_failure_restores_current_release
test_apply_success_promotes_candidate_after_verification
test_manual_rollback_start_failure_preserves_release_state
test_manual_rollback_success_swaps_release_state_after_verification

printf 'PASS: wildlife deployment failure-path tests completed.\n'
