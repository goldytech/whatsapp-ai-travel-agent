# Deployment Guide

For the platform overview, architecture, and roadmap, start with the [main README](../README.md).

## Aspire-generated Docker Compose deployment

The repository supports **Aspire-generated Docker Compose** for VPS/self-hosted deployments.

### Publish the travel plugin

```bash
dotnet publish src/Verticals/AgentForge.Verticals.Travel/AgentForge.Verticals.Travel.csproj
```

By default this writes the runtime-loadable travel plugin to:

```text
artifacts/plugins/travel/
```

### Generate the Compose artifacts

```bash
aspire publish --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj -o artifacts/aspire-output
```

This generates:

- `artifacts/aspire-output/docker-compose.yaml`
- a Compose model for the published services plus Aspire's optional `compose-dashboard`
- a persistent `waha-sessions` Docker volume
- bind mounts that project `artifacts/plugins/travel` into both app services at `/app/plugins/travel`
- optional read-only customer-config bind mounts when `CUSTOMER_CONFIG_SOURCE_PATH` is set
- an `.env` template whose secret-backed values and image names must be supplied at deployment time

### Deployment notes

- `AgentForge.WebApi` and `AgentForge.McpHost` receive `VERTICAL_ID`, `VERTICAL_PLUGIN_ROOT`, and `VERTICAL_PLUGIN_PATH` automatically in publish mode.
- If `CUSTOMER_CONFIG_SOURCE_PATH` is set on the AppHost, both hosts also receive `CUSTOMER_CONFIG_PATH` and the mounted folder is available read-only for runtime descriptor overrides.
- During local `aspire start`, `vertical-plugin-path` and `customer-config-path` are exposed as optional Aspire parameter overrides with friendly dashboard labels/placeholders, but they default to blank so normal startup does not require dashboard input.
- `DevTunnel` and `MCP Inspector` are intentionally **local-only** and are not included in published Compose output.
- Published Compose exposes the `webhook` service on `WEBHOOK_HOST_PORT` and the OpenWA dashboard on `OPENWA_DASHBOARD_HOST_PORT` so you can front them with a VPS public IP, reverse proxy, or an external tunnel such as Microsoft Dev Tunnels, ngrok, or Cloudflare Tunnel.
- To publish a different vertical later, publish that plugin to `artifacts/plugins/<vertical-id>/` and set `VERTICAL_ID` (and optionally `VERTICAL_PLUGIN_SOURCE_PATH` and `CUSTOMER_CONFIG_SOURCE_PATH`) before running `aspire publish`.

## Production deployment checklist

1. Publish the active vertical plugin:

   ```bash
   dotnet publish src/Verticals/AgentForge.Verticals.Travel/AgentForge.Verticals.Travel.csproj -c Release -o artifacts/plugins/travel
   ```

2. Build the two .NET service images that the generated Compose file expects:

   ```bash
   dotnet publish src/AgentForge.McpHost/AgentForge.McpHost.csproj -c Release /t:PublishContainer -p:ContainerRepository=agentforge-mcpserver-local -p:ContainerImageTag=deploytest
   dotnet publish src/AgentForge.WebApi/AgentForge.WebApi.csproj -c Release /t:PublishContainer -p:ContainerRepository=agentforge-webhook-local -p:ContainerImageTag=deploytest
   ```

3. Export the runtime values the generated `.env` leaves blank:

   ```bash
   export AI_FOUNDRY='Endpoint=https://...;Key=...'
   export COMPOSEDASHBOARDBROWSERTOKEN='set-a-strong-dashboard-token'
   export OPENWAAPIKEY='...'
   export OPENWAENCRYPTIONKEY='generate-a-long-random-secret'
   export OPENWAWEBHOOKSECRET='generate-a-second-long-random-secret'
   export OPENWAPOSTGRESPASSWORD='set-a-strong-database-password'
   export WEBHOOK_BASE_URL='https://your-public-host-or-tunnel'
   export MCPSERVER_IMAGE='agentforge-mcpserver-local:deploytest'
   export WEBHOOK_IMAGE='agentforge-webhook-local:deploytest'
   export MCPSERVER_PORT='8081'
   export WEBHOOK_PORT='8080'
   export WEBHOOK_HOST_PORT='8080'
   export OPENWA_DASHBOARD_HOST_PORT='2886'
   ```

   The published Aspire dashboard is configured with `Dashboard__ApplicationName=AgentForge` and
   `Dashboard__Frontend__AuthMode=BrowserToken`. Set `COMPOSEDASHBOARDBROWSERTOKEN` before
   `docker compose up` so the dashboard uses your chosen login token instead of a runtime-generated one.
   Published deployments also expect `WEBHOOK_BASE_URL` because Aspire dev tunnels are not part of
   `aspire publish`; `WEBHOOK_HOST_PORT` is the host-side port that exposes the webhook container and
   `OPENWA_DASHBOARD_HOST_PORT` exposes the OpenWA dashboard at `http://<host>:<port>/`.

4. Start the published stack:

   ```bash
   docker compose -p agentforge-prodtest -f artifacts/aspire-output/docker-compose.yaml up -d
   ```

5. Restore or authenticate OpenWA's `default` session before testing outbound replies. A fresh `openwa-data` volume will not send messages until the session is authenticated.

   If you want to scan the QR code through the published dashboard instead of restoring an existing
   session volume, open `http://<host>:${OPENWA_DASHBOARD_HOST_PORT}/` (or your reverse-proxied
   HTTPS hostname). If the UI prompts for credentials, use the configured `OPENWAAPIKEY`, then
   authenticate or inspect the `default` session there.

   If you restored an existing authenticated OpenWA volume, verify the session explicitly:

   ```bash
   docker exec agentforge-prodtest-openwa-1 node -e "fetch('http://127.0.0.1:2785/api/sessions/default',{headers:{'X-Api-Key': process.argv[1],'X-API-KEY': process.argv[1],'Accept':'application/json'}}).then(async r=>{console.log(r.status);console.log(await r.text());})" "$OPENWAAPIKEY"
   ```

   If it is disconnected or stopped, restart it:

   ```bash
   docker exec agentforge-prodtest-openwa-1 node -e "fetch('http://127.0.0.1:2785/api/sessions/default/restart',{method:'POST',headers:{'X-Api-Key': process.argv[1],'X-API-KEY': process.argv[1],'Accept':'application/json'}}).then(async r=>{console.log(r.status);console.log(await r.text());})" "$OPENWAAPIKEY"
   ```

6. Validate the end-to-end message path by sending a signed webhook event into the deployed `webhook` service from inside the Compose network:

   ```bash
   docker exec agentforge-prodtest-openwa-1 node -e "const crypto=require('crypto'); const body=JSON.stringify({event:'message.received',sessionId:'default',deliveryId:'test-'+Date.now(),data:{id:'msg-'+Date.now(),chatId:'919825318335@c.us',from:'919825318335@c.us',fromMe:false,body:'Hi what is your name',type:'chat',hasMedia:false}}); const sig='sha256='+crypto.createHmac('sha256', process.argv[1]).update(body).digest('hex'); fetch('http://webhook:8080/webhook',{method:'POST',headers:{'Content-Type':'application/json','X-OpenWA-Signature':sig},body}).then(async r=>{console.log(r.status);console.log(await r.text());}).catch(err=>{console.error(err);process.exit(1);});" "$OPENWAWEBHOOKSECRET"
   ```

7. Confirm the deployed services processed the message and returned a WhatsApp reply:

   ```bash
   docker compose -p agentforge-prodtest -f artifacts/aspire-output/docker-compose.yaml logs --tail=120 webhook mcpserver openwa
   ```

   A successful run shows:

   - `AgentForge.WebApi` calling Azure AI Foundry successfully
   - `AgentForge.WebApi` sending `POST http://openwa:2785/api/sessions/default/messages/send-text`
   - OpenWA accepting the send request without duplicate retries

If you need OpenWA media delivery or automatic webhook registration outside local Aspire, provide a public `WEBHOOK_BASE_URL` for the deployed `webhook` service.

## Local prospect demo with a manual public tunnel

If you want a **production-like published Compose demo on your local machine**, the best-supported
Aspire-native approach is to start an external tunnel yourself and point `WEBHOOK_BASE_URL` at it.
For Microsoft Dev Tunnels, use the official `devtunnel` CLI against the host-published webhook port:

```bash
devtunnel user login -g
devtunnel host -p 8080 --allow-anonymous
```

Use the `https://...devtunnels.ms` URL that the CLI prints as `WEBHOOK_BASE_URL`, then start or
restart the published Compose stack.

Two good alternatives if you prefer other tunnel providers:

- `cloudflared tunnel --url http://localhost:8080` for the fastest no-account quick demo
- `ngrok http 8080` if you already use ngrok and want its traffic policies / managed domains

If you want the **fully Aspire-managed Dev Tunnel experience**, use `aspire start` instead of the
published Docker Compose bundle. That is the mode where Aspire provisions and wires the tunnel for you.

## Repeatable Cloudflare demo workflow for a local Mac mini

If `qloop.tech` is already active on your Cloudflare account, the most repeatable no-VPS demo flow is
to use a **named Cloudflare Tunnel** and deploy one vertical at a time behind two per-vertical hostnames
such as `travel-demo.qloop.tech` for the webhook and `travel-openwa-demo.qloop.tech` for the OpenWA dashboard.

**Prerequisites**

- `cloudflared` installed locally
  - macOS: `brew install cloudflared`
- Docker / Docker Compose
- .NET SDK plus Aspire CLI
- a local Cloudflare login completed once with `cloudflared tunnel login`
- runtime secrets available either as exported env vars or in a local file at
  `~/.config/agentforge-demo/runtime.env`

**Bootstrap once**

```bash
scripts/bootstrap-cloudflare-demo.sh
```

That script:

- prompts for the root domain and tunnel name (defaults: `qloop.tech`, `agentforge-demo`)
- runs `cloudflared tunnel login` if `~/.cloudflared/cert.pem` is missing
- creates or reuses the named tunnel
- stores local non-secret tunnel metadata in `~/.config/agentforge-demo/cloudflare.env`

Create a local runtime env file if you do not want to export the values every time:

```bash
mkdir -p ~/.config/agentforge-demo
cat > ~/.config/agentforge-demo/runtime.env <<'EOF'
AI_FOUNDRY='Endpoint=https://...;Key=...'
COMPOSEDASHBOARDBROWSERTOKEN='set-a-strong-dashboard-token'
OPENWAAPIKEY='...'
OPENWAENCRYPTIONKEY='generate-a-long-random-secret'
OPENWAWEBHOOKSECRET='generate-a-second-long-random-secret'
OPENWAPOSTGRESPASSWORD='set-a-strong-database-password'
MCPSERVER_IMAGE='agentforge-mcpserver-local:deploytest'
WEBHOOK_IMAGE='agentforge-webhook-local:deploytest'
MCPSERVER_PORT='8081'
WEBHOOK_PORT='8080'
WEBHOOK_HOST_PORT='8080'
OPENWA_DASHBOARD_HOST_PORT='2886'
EOF
```

**Deploy one vertical**

```bash
scripts/deploy-demo-vertical.sh travel
```

The deploy script:

- publishes `artifacts/plugins/<vertical-id>`
- rebuilds the `mcpserver` and `webhook` container images
- runs `aspire publish`
- starts the published Compose stack as `agentforge-<vertical-id>-demo`
- assigns `https://<vertical-id>-demo.qloop.tech` to the webhook and
  `https://<vertical-id>-openwa-demo.qloop.tech/` to OpenWA
- starts a managed local `cloudflared` process and stores its PID/log under
  `~/.config/agentforge-demo/`

Use the configured `OPENWAAPIKEY` in the dashboard UI if it prompts for API access.

By default this workflow is optimized for **one active local demo at a time** because the tunnel config
routes the current demo's webhook and OpenWA dashboard hostnames to the current host-published ports.

**Stop the current demo**

```bash
scripts/stop-demo-vertical.sh travel
```

If you omit the vertical ID, the stop script uses the most recent deployment state from
`~/.config/agentforge-demo/current-demo.env`.
