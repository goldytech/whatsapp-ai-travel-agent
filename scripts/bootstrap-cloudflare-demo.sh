#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/demo-common.sh
source "${SCRIPT_DIR}/lib/demo-common.sh"

require_cloudflared
ensure_directory "$DEMO_STATE_DIR"
ensure_directory "$DEMO_CLOUDFLARED_DIR"

DEMO_ROOT_DOMAIN="$(prompt_for_value "Demo root domain" "${DEMO_ROOT_DOMAIN:-qloop.tech}")"
CLOUDFLARE_TUNNEL_NAME="$(prompt_for_value "Cloudflare tunnel name" "${CLOUDFLARE_TUNNEL_NAME:-agentforge-demo}")"

if [[ ! -f "${DEMO_CLOUDFLARED_DIR}/cert.pem" ]]; then
    log "No Cloudflare cert.pem found. Running cloudflared tunnel login..."
    cloudflared tunnel login
fi

existing_tunnel_id=""

if [[ -f "$DEMO_STATE_FILE" ]]; then
    # shellcheck disable=SC1090
    source "$DEMO_STATE_FILE"
    existing_tunnel_id="${CLOUDFLARE_TUNNEL_ID:-}"
fi

if [[ -z "$existing_tunnel_id" ]]; then
    if cloudflared tunnel info "$CLOUDFLARE_TUNNEL_NAME" >/dev/null 2>&1; then
        existing_tunnel_id="$(cloudflared tunnel list | awk -v name="$CLOUDFLARE_TUNNEL_NAME" '$2 == name { print $1; exit }')"
        [[ -n "$existing_tunnel_id" ]] || die "Tunnel '${CLOUDFLARE_TUNNEL_NAME}' exists, but its UUID could not be determined."
        log "Reusing existing tunnel ${CLOUDFLARE_TUNNEL_NAME} (${existing_tunnel_id})"
    else
        log "Creating Cloudflare tunnel ${CLOUDFLARE_TUNNEL_NAME}"
        create_output="$(cloudflared tunnel create "$CLOUDFLARE_TUNNEL_NAME" 2>&1)" || die "$create_output"
        existing_tunnel_id="$(printf '%s\n' "$create_output" | grep -Eo '[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}' | head -n 1)"
        [[ -n "$existing_tunnel_id" ]] || die "Tunnel creation succeeded but the tunnel UUID could not be parsed."
        printf '%s\n' "$create_output"
    fi
fi

credentials_file="${DEMO_CLOUDFLARED_DIR}/${existing_tunnel_id}.json"
[[ -f "$credentials_file" ]] || die "Missing tunnel credentials file: ${credentials_file}"

write_file_atomically "$DEMO_STATE_FILE" <<EOF
CLOUDFLARE_TUNNEL_NAME=${CLOUDFLARE_TUNNEL_NAME}
CLOUDFLARE_TUNNEL_ID=${existing_tunnel_id}
DEMO_ROOT_DOMAIN=${DEMO_ROOT_DOMAIN}
EOF

log "Bootstrap complete."
log "State file: ${DEMO_STATE_FILE}"
log "Tunnel credentials: ${credentials_file}"
log "Next: add runtime secrets to ${DEMO_RUNTIME_ENV_FILE} or export them in your shell, then run scripts/deploy-demo-vertical.sh <vertical-id>"
