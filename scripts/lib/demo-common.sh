#!/usr/bin/env bash

set -euo pipefail

readonly DEMO_LIB_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly DEMO_REPO_ROOT="$(cd -- "${DEMO_LIB_DIR}/../.." && pwd)"
readonly DEMO_STATE_DIR="${AGENTFORGE_DEMO_STATE_DIR:-$HOME/.config/agentforge-demo}"
readonly DEMO_CLOUDFLARED_DIR="${CLOUDFLARED_CONFIG_DIR:-$HOME/.cloudflared}"
readonly DEMO_STATE_FILE="${AGENTFORGE_DEMO_STATE_FILE:-$DEMO_STATE_DIR/cloudflare.env}"
readonly DEMO_RUNTIME_ENV_FILE="${AGENTFORGE_DEMO_ENV_FILE:-$DEMO_STATE_DIR/runtime.env}"
readonly DEMO_CURRENT_FILE="${AGENTFORGE_CURRENT_DEMO_FILE:-$DEMO_STATE_DIR/current-demo.env}"
readonly DEMO_TUNNEL_CONFIG="${AGENTFORGE_TUNNEL_CONFIG_FILE:-$DEMO_STATE_DIR/cloudflared-demo.yml}"
readonly DEMO_TUNNEL_PID_FILE="${AGENTFORGE_TUNNEL_PID_FILE:-$DEMO_STATE_DIR/cloudflared-demo.pid}"
readonly DEMO_TUNNEL_LOG_FILE="${AGENTFORGE_TUNNEL_LOG_FILE:-$DEMO_STATE_DIR/cloudflared-demo.log}"

log() {
    printf '[agentforge-demo] %s\n' "$*"
}

warn() {
    printf '[agentforge-demo] WARNING: %s\n' "$*" >&2
}

die() {
    printf '[agentforge-demo] ERROR: %s\n' "$*" >&2
    exit 1
}

ensure_directory() {
    mkdir -p "$1"
}

require_command() {
    local command_name="$1"

    command -v "$command_name" >/dev/null 2>&1 || die "Missing required command: ${command_name}"
}

cloudflared_install_hint() {
    case "$(uname -s)" in
        Darwin)
            printf 'Install cloudflared with Homebrew: brew install cloudflared\n'
            ;;
        Linux)
            printf 'Install cloudflared from Cloudflare packages or downloads: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/downloads/\n'
            ;;
        *)
            printf 'Install cloudflared from: https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/downloads/\n'
            ;;
    esac
}

require_cloudflared() {
    if command -v cloudflared >/dev/null 2>&1; then
        return
    fi

    die "$(cloudflared_install_hint)"
}

canonicalize_id() {
    printf '%s' "$1" | tr '[:upper:]' '[:lower:]' | tr -cd '[:alnum:]'
}

prompt_for_value() {
    local prompt="$1"
    local current_value="${2:-}"

    if [[ -n "$current_value" ]]; then
        printf '%s' "$current_value"
        return
    fi

    read -r -p "${prompt}: " current_value
    printf '%s' "$current_value"
}

load_demo_state() {
    [[ -f "$DEMO_STATE_FILE" ]] || die "Missing demo bootstrap state at ${DEMO_STATE_FILE}. Run scripts/bootstrap-cloudflare-demo.sh first."
    # shellcheck disable=SC1090
    source "$DEMO_STATE_FILE"

    : "${CLOUDFLARE_TUNNEL_NAME:?Missing CLOUDFLARE_TUNNEL_NAME in ${DEMO_STATE_FILE}}"
    : "${CLOUDFLARE_TUNNEL_ID:?Missing CLOUDFLARE_TUNNEL_ID in ${DEMO_STATE_FILE}}"
    : "${DEMO_ROOT_DOMAIN:?Missing DEMO_ROOT_DOMAIN in ${DEMO_STATE_FILE}}"
}

load_runtime_env_file_if_present() {
    if [[ -f "$DEMO_RUNTIME_ENV_FILE" ]]; then
        # shellcheck disable=SC1090
        source "$DEMO_RUNTIME_ENV_FILE"
    fi
}

require_env_var() {
    local env_name="$1"
    [[ -n "${!env_name:-}" ]] || die "Missing required environment variable: ${env_name}"
}

write_file_atomically() {
    local target_path="$1"
    local temp_file
    temp_file="$(mktemp "${target_path}.tmp.XXXXXX")"
    cat >"$temp_file"
    mv "$temp_file" "$target_path"
}

read_pid_file() {
    [[ -f "$DEMO_TUNNEL_PID_FILE" ]] || return 1
    cat "$DEMO_TUNNEL_PID_FILE"
}

process_matches_pid() {
    local pid="$1"

    kill -0 "$pid" >/dev/null 2>&1 || return 1
    ps -p "$pid" -o command= | grep -F "cloudflared tunnel" >/dev/null 2>&1
}

stop_cloudflared_process() {
    local pid

    pid="$(read_pid_file 2>/dev/null || true)"
    [[ -n "$pid" ]] || return 0

    if process_matches_pid "$pid"; then
        log "Stopping existing cloudflared process ${pid}"
        kill "$pid"

        for _ in {1..20}; do
            if ! kill -0 "$pid" >/dev/null 2>&1; then
                break
            fi
            sleep 1
        done

        if kill -0 "$pid" >/dev/null 2>&1; then
            die "cloudflared process ${pid} did not stop cleanly."
        fi
    else
        warn "PID file pointed to ${pid}, but no matching cloudflared process is running."
    fi

    rm -f "$DEMO_TUNNEL_PID_FILE"
}

start_cloudflared_process() {
    local tunnel_name="$1"
    local config_path="$2"

    ensure_directory "$DEMO_STATE_DIR"
    stop_cloudflared_process

    : >"$DEMO_TUNNEL_LOG_FILE"

    nohup cloudflared tunnel --config "$config_path" run "$tunnel_name" >>"$DEMO_TUNNEL_LOG_FILE" 2>&1 &
    local pid=$!
    printf '%s\n' "$pid" >"$DEMO_TUNNEL_PID_FILE"

    sleep 3

    process_matches_pid "$pid" || {
        tail -n 50 "$DEMO_TUNNEL_LOG_FILE" >&2 || true
        die "cloudflared tunnel process failed to stay running."
    }
}

wait_for_local_http() {
    local url="$1"
    local attempts="${2:-30}"

    for _ in $(seq 1 "$attempts"); do
        if curl --silent --show-error --output /dev/null "$url"; then
            return 0
        fi

        sleep 1
    done

    die "Timed out waiting for local endpoint ${url}"
}

find_vertical_project() {
    local vertical_id="$1"
    local wanted
    wanted="$(canonicalize_id "$vertical_id")"
    local matches=()
    local project_path

    while IFS= read -r project_path; do
        local project_name="${project_path##*/}"
        project_name="${project_name%.csproj}"
        local suffix="${project_name##*.}"
        if [[ "$(canonicalize_id "$suffix")" == "$wanted" ]]; then
            matches+=("$project_path")
        fi
    done < <(find "$DEMO_REPO_ROOT/src/Verticals" -name '*.csproj' -print)

    if [[ "${#matches[@]}" -eq 0 ]]; then
        die "Could not find a vertical project for '${vertical_id}'. Set VERTICAL_PROJECT_PATH if the project naming does not follow AgentForge.Verticals.<Name>."
    fi

    if [[ "${#matches[@]}" -gt 1 ]]; then
        die "Found multiple vertical projects for '${vertical_id}': ${matches[*]}"
    fi

    printf '%s\n' "${matches[0]}"
}

parse_image_reference() {
    local image_ref="$1"
    local default_tag="$2"
    local remainder="${image_ref##*/}"

    if [[ "$remainder" == *:* ]]; then
        IMAGE_REPOSITORY="${image_ref%:*}"
        IMAGE_TAG="${image_ref##*:}"
    else
        IMAGE_REPOSITORY="$image_ref"
        IMAGE_TAG="$default_tag"
    fi
}

save_current_demo_state() {
    local vertical_id="$1"
    local compose_project="$2"
    local webhook_hostname="$3"
    local webhook_host_port="$4"
    local waha_hostname="$5"
    local waha_dashboard_host_port="$6"

    write_file_atomically "$DEMO_CURRENT_FILE" <<EOF
VERTICAL_ID=${vertical_id}
COMPOSE_PROJECT=${compose_project}
DEMO_HOSTNAME=${webhook_hostname}
WEBHOOK_HOST_PORT=${webhook_host_port}
WAHA_DEMO_HOSTNAME=${waha_hostname}
WAHA_DASHBOARD_HOST_PORT=${waha_dashboard_host_port}
EOF
}
