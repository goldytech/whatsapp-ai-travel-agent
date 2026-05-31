#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=scripts/lib/demo-common.sh
source "${SCRIPT_DIR}/lib/demo-common.sh"

require_cloudflared
require_command docker
require_command dotnet
require_command aspire
require_command curl
load_demo_state
load_runtime_env_file_if_present

vertical_id="${1:-}"
vertical_id="$(prompt_for_value "Vertical ID" "$vertical_id")"
[[ -n "$vertical_id" ]] || die "Vertical ID is required."

customer_config_source_path="${CUSTOMER_CONFIG_SOURCE_PATH:-}"
webhook_host_port="${WEBHOOK_HOST_PORT:-8080}"
openwa_dashboard_host_port="${OPENWA_DASHBOARD_HOST_PORT:-2886}"
compose_project="${COMPOSE_PROJECT_NAME:-agentforge-${vertical_id}-demo}"
hostname="${DEMO_HOSTNAME_OVERRIDE:-${vertical_id}-demo.${DEMO_ROOT_DOMAIN}}"
openwa_hostname="${OPENWA_DEMO_HOSTNAME_OVERRIDE:-${vertical_id}-openwa-demo.${DEMO_ROOT_DOMAIN}}"

MCPSERVER_IMAGE="${MCPSERVER_IMAGE:-agentforge-mcpserver-local:deploytest}"
WEBHOOK_IMAGE="${WEBHOOK_IMAGE:-agentforge-webhook-local:deploytest}"
COMPOSEDASHBOARDBROWSERTOKEN="${COMPOSEDASHBOARDBROWSERTOKEN:-}"
AI_FOUNDRY="${AI_FOUNDRY:-}"
OPENWAAPIKEY="${OPENWAAPIKEY:-}"
OPENWAENCRYPTIONKEY="${OPENWAENCRYPTIONKEY:-}"
OPENWAWEBHOOKSECRET="${OPENWAWEBHOOKSECRET:-}"
OPENWAPOSTGRESPASSWORD="${OPENWAPOSTGRESPASSWORD:-}"
MCPSERVER_PORT="${MCPSERVER_PORT:-8081}"
WEBHOOK_PORT="${WEBHOOK_PORT:-8080}"
WEBHOOK_BASE_URL="https://${hostname}"
OPENWA_DASHBOARD_HOST_PORT="${openwa_dashboard_host_port}"

require_env_var AI_FOUNDRY
require_env_var COMPOSEDASHBOARDBROWSERTOKEN
require_env_var OPENWAAPIKEY
require_env_var OPENWAENCRYPTIONKEY
require_env_var OPENWAWEBHOOKSECRET
require_env_var OPENWAPOSTGRESPASSWORD
require_env_var MCPSERVER_IMAGE
require_env_var WEBHOOK_IMAGE
require_env_var MCPSERVER_PORT
require_env_var WEBHOOK_PORT
require_env_var WEBHOOK_HOST_PORT
require_env_var OPENWA_DASHBOARD_HOST_PORT

if [[ "$webhook_host_port" == "$openwa_dashboard_host_port" ]]; then
    die "WEBHOOK_HOST_PORT (${webhook_host_port}) and OPENWA_DASHBOARD_HOST_PORT (${openwa_dashboard_host_port}) must be different."
fi

if [[ "$hostname" == "$openwa_hostname" ]]; then
    die "The webhook hostname and OpenWA hostname must be different."
fi

vertical_project="${VERTICAL_PROJECT_PATH:-$(find_vertical_project "$vertical_id")}"
plugin_output_dir="${DEMO_REPO_ROOT}/artifacts/plugins/${vertical_id}"
compose_output_dir="${DEMO_REPO_ROOT}/artifacts/aspire-output"

parse_image_reference "$MCPSERVER_IMAGE" "deploytest"
mcp_repository="$IMAGE_REPOSITORY"
mcp_tag="$IMAGE_TAG"

parse_image_reference "$WEBHOOK_IMAGE" "deploytest"
webhook_repository="$IMAGE_REPOSITORY"
webhook_tag="$IMAGE_TAG"

log "Publishing vertical plugin ${vertical_id}"
dotnet publish "$vertical_project" -c Release -o "$plugin_output_dir"

log "Building container image ${MCPSERVER_IMAGE}"
dotnet publish "${DEMO_REPO_ROOT}/src/AgentForge.McpHost/AgentForge.McpHost.csproj" \
    -c Release \
    /t:PublishContainer \
    -p:ContainerRepository="$mcp_repository" \
    -p:ContainerImageTag="$mcp_tag"

log "Building container image ${WEBHOOK_IMAGE}"
dotnet publish "${DEMO_REPO_ROOT}/src/AgentForge.WebApi/AgentForge.WebApi.csproj" \
    -c Release \
    /t:PublishContainer \
    -p:ContainerRepository="$webhook_repository" \
    -p:ContainerImageTag="$webhook_tag"

log "Publishing Aspire compose output"
(
    cd "$DEMO_REPO_ROOT"
    export VERTICAL_ID="$vertical_id"
    export VERTICAL_PLUGIN_SOURCE_PATH="$plugin_output_dir"
    if [[ -n "$customer_config_source_path" ]]; then
        export CUSTOMER_CONFIG_SOURCE_PATH="$customer_config_source_path"
    fi
    aspire publish --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj -o "$compose_output_dir"
)

log "Starting Docker Compose project ${compose_project}"
(
    cd "$DEMO_REPO_ROOT"
    export AI_FOUNDRY
    export COMPOSEDASHBOARDBROWSERTOKEN
    export OPENWAAPIKEY
    export OPENWAENCRYPTIONKEY
    export OPENWAWEBHOOKSECRET
    export OPENWAPOSTGRESPASSWORD
    export WEBHOOK_BASE_URL
    export WEBHOOK_HOST_PORT="$webhook_host_port"
    export OPENWA_DASHBOARD_HOST_PORT="$openwa_dashboard_host_port"
    export MCPSERVER_IMAGE
    export WEBHOOK_IMAGE
    export MCPSERVER_PORT
    export WEBHOOK_PORT
    docker compose -p "$compose_project" -f "$compose_output_dir/docker-compose.yaml" up -d
)

wait_for_local_http "http://127.0.0.1:${webhook_host_port}/"
wait_for_local_http "http://127.0.0.1:${openwa_dashboard_host_port}/"

log "Ensuring DNS route for ${hostname}"
cloudflared tunnel route dns --overwrite-dns "$CLOUDFLARE_TUNNEL_NAME" "$hostname"
log "Ensuring DNS route for ${openwa_hostname}"
cloudflared tunnel route dns --overwrite-dns "$CLOUDFLARE_TUNNEL_NAME" "$openwa_hostname"

credentials_file="${DEMO_CLOUDFLARED_DIR}/${CLOUDFLARE_TUNNEL_ID}.json"
[[ -f "$credentials_file" ]] || die "Missing tunnel credentials file: ${credentials_file}"

write_file_atomically "$DEMO_TUNNEL_CONFIG" <<EOF
tunnel: ${CLOUDFLARE_TUNNEL_ID}
credentials-file: ${credentials_file}
ingress:
  # Cloudflared matches ingress rules in order; keep the catch-all 404 handler last.
  - hostname: ${hostname}
    service: http://localhost:${webhook_host_port}
  - hostname: ${openwa_hostname}
    service: http://localhost:${openwa_dashboard_host_port}
  - service: http_status:404
EOF

cloudflared tunnel --config "$DEMO_TUNNEL_CONFIG" ingress validate
start_cloudflared_process "$CLOUDFLARE_TUNNEL_NAME" "$DEMO_TUNNEL_CONFIG"
save_current_demo_state "$vertical_id" "$compose_project" "$hostname" "$webhook_host_port" "$openwa_hostname" "$openwa_dashboard_host_port"

log "Demo deployed."
log "Public URL: ${WEBHOOK_BASE_URL}"
log "OpenWA Dashboard URL: https://${openwa_hostname}/"
log "Compose project: ${compose_project}"
log "Tunnel log: ${DEMO_TUNNEL_LOG_FILE}"
