#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly SCRIPT_DIR
DEPLOY_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
readonly DEPLOY_DIR

fail() {
    printf '[wildlife-bootstrap] ERROR: %s\n' "$*" >&2
    exit 1
}

[[ "${EUID}" -eq 0 ]] || fail "Run this script with sudo."
[[ "$(uname -s)" == "Linux" ]] || fail "This bootstrap supports Linux only."
grep -qi microsoft /proc/sys/kernel/osrelease || fail "This bootstrap is intended for WSL 2."
command -v docker > /dev/null || fail "Docker Engine is not installed."
docker compose version > /dev/null || fail "The Docker Compose plugin is not installed."
command -v tailscale > /dev/null || fail "Tailscale is not installed."
command -v visudo > /dev/null || fail "visudo is not installed."
command -v curl > /dev/null || fail "curl is not installed."
command -v flock > /dev/null || fail "flock is not installed."

if ! id deploy > /dev/null 2>&1; then
    useradd --create-home --shell /bin/bash deploy
fi

if id -nG deploy | tr ' ' '\n' | grep -qx docker; then
    fail "The deploy user must not belong to the docker group."
fi

install -d -o root -g root -m 0755 /opt/wildlife /opt/wildlife/infra
install -d -o root -g root -m 0700 /opt/wildlife/state /etc/wildlife /var/backups/wildlife
install -d -o deploy -g deploy -m 0700 /var/lib/wildlife/inbox

install -o root -g root -m 0644 "${DEPLOY_DIR}/compose.production.yml" /opt/wildlife/infra/compose.production.yml
install -o root -g root -m 0644 "${DEPLOY_DIR}/Caddyfile" /opt/wildlife/infra/Caddyfile
install -o root -g root -m 0750 "${SCRIPT_DIR}/wildlife-deploy" /usr/local/sbin/wildlife-deploy
visudo -cf "${SCRIPT_DIR}/wildlife-sudoers" > /dev/null
install -o root -g root -m 0440 "${SCRIPT_DIR}/wildlife-sudoers" /etc/sudoers.d/wildlife-deploy

for environment_file in db.env app.env deploy.env; do
    if [[ ! -e "/etc/wildlife/${environment_file}" ]]; then
        install -o root -g root -m 0600 /dev/null "/etc/wildlife/${environment_file}"
    fi
done

printf '%s\n' \
    'Bootstrap files were installed.' \
    'Next steps:' \
    '1. Fill /etc/wildlife/db.env, app.env, and deploy.env as root.' \
    '2. Authenticate root to private GHCR with a read:packages PAT classic.' \
    '3. Enable Tailscale SSH and tag this node as tag:wildlife-prod.' \
    '4. Apply the least-privilege tailnet policy from deploy/examples.' \
    '5. Point the GSMVS HTTPS subdomain to this server on internal TCP port 18080.' \
    '6. Configure the GitHub production environment and merge through the branch flow.'
