#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/demo-common.sh
source "${SCRIPT_DIR}/lib/demo-common.sh"

require_command docker

vertical_id="${1:-}"
compose_project=""

if [[ -f "$DEMO_CURRENT_FILE" ]]; then
    # shellcheck disable=SC1090
    source "$DEMO_CURRENT_FILE"
fi

if [[ -n "$vertical_id" ]]; then
    compose_project="agentforge-${vertical_id}-demo"
elif [[ -n "${COMPOSE_PROJECT:-}" ]]; then
    compose_project="$COMPOSE_PROJECT"
    vertical_id="${VERTICAL_ID:-}"
else
    die "No running demo state found. Pass a vertical ID explicitly."
fi

stop_cloudflared_process

if [[ -f "${DEMO_REPO_ROOT}/artifacts/aspire-output/docker-compose.yaml" ]]; then
    (
        cd "$DEMO_REPO_ROOT"
        docker compose -p "$compose_project" -f artifacts/aspire-output/docker-compose.yaml down
    )
else
    warn "No compose file found under artifacts/aspire-output; skipped docker compose down."
fi

if [[ -f "$DEMO_CURRENT_FILE" ]]; then
    rm -f "$DEMO_CURRENT_FILE"
fi

log "Stopped demo ${vertical_id:-$compose_project}"
