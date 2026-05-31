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
- Published Compose exposes the `webhook` service on `WEBHOOK_HOST_PORT` and the WAHA dashboard on `WAHA_DASHBOARD_HOST_PORT` so you can front them with a VPS public IP, reverse proxy, or an external tunnel such as Microsoft Dev Tunnels, ngrok, or Cloudflare Tunnel.
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
   export WAHAAPIKEY='...'
   export WAHADASHBOARDPASSWORD='...'
   export WAHASWAGGERPASSWORD='...'
   export WAHAWEBHOOKSECRET='...'
   export WEBHOOK_BASE_URL='https://your-public-host-or-tunnel'
   export MCPSERVER_IMAGE='agentforge-mcpserver-local:deploytest'
   export WEBHOOK_IMAGE='agentforge-webhook-local:deploytest'
   export MCPSERVER_PORT='8081'
   export WEBHOOK_PORT='8080'
   export WEBHOOK_HOST_PORT='8080'
   export WAHA_DASHBOARD_HOST_PORT='3000'
   ```

   The published Aspire dashboard is configured with `Dashboard__ApplicationName=AgentForge` and
   `Dashboard__Frontend__AuthMode=BrowserToken`. Set `COMPOSEDASHBOARDBROWSERTOKEN` before
   `docker compose up` so the dashboard uses your chosen login token instead of a runtime-generated one.
   Published deployments also expect `WEBHOOK_BASE_URL` because Aspire dev tunnels are not part of
   `aspire publish`; `WEBHOOK_HOST_PORT` is the host-side port that exposes the webhook container and
   `WAHA_DASHBOARD_HOST_PORT` exposes the WAHA dashboard at `http://<host>:<port>/dashboard`.

4. Start the published stack:

   ```bash
   docker compose -p agentforge-prodtest -f artifacts/aspire-output/docker-compose.yaml up -d
   ```

5. Restore or authenticate WAHA's `default` session before testing outbound replies. A fresh `waha-sessions` volume will not send messages until the session is started.

   If you want to scan the QR code through the published dashboard instead of restoring an existing
   session volume, open `http://<host>:${WAHA_DASHBOARD_HOST_PORT}/dashboard` (or your reverse-proxied
   HTTPS hostname), sign in as `admin` with `WAHADASHBOARDPASSWORD`, then configure the `default`
   session there.

   If you restored an existing authenticated WAHA volume, start the session explicitly:

   ```bash
   docker exec agentforge-prodtest-waha-1 node -e "fetch('http://127.0.0.1:3000/api/sessions/default/start',{method:'POST',headers:{'X-Api-Key': process.argv[1],'Accept':'application/json'}}).then(async r=>{console.log(r.status);console.log(await r.text());})" "$WAHAAPIKEY"
   ```

   Verify WAHA sees the session:

   ```bash
   docker exec agentforge-prodtest-waha-1 node -e "fetch('http://127.0.0.1:3000/api/sessions',{headers:{'X-Api-Key': process.argv[1]}}).then(r=>r.text()).then(console.log)" "$WAHAAPIKEY"
   ```

6. Validate the end-to-end message path by sending a signed webhook event into the deployed `webhook` service from inside the Compose network:

   ```bash
   docker exec agentforge-prodtest-waha-1 node -e "const crypto=require('crypto'); const body=JSON.stringify({event:'message',session:'default',payload:{id:'wamid.test.'+Date.now(),timestamp:Math.floor(Date.now()/1000),from:'919825318335@c.us',fromMe:false,to:'916355118335@c.us',body:'Hi what is your name',type:'chat',hasMedia:false,_data:{}}}); const sig=crypto.createHmac('sha512', process.argv[1]).update(body).digest('hex'); fetch('http://webhook:8080/webhook',{method:'POST',headers:{'Content-Type':'application/json','X-Webhook-Hmac':sig,'X-Webhook-Hmac-Algorithm':'sha512'},body}).then(async r=>{console.log(r.status);console.log(await r.text());}).catch(err=>{console.error(err);process.exit(1);});" "$WAHAWEBHOOKSECRET"
   ```

7. Confirm the deployed services processed the message and returned a WhatsApp reply:

   ```bash
   docker compose -p agentforge-prodtest -f artifacts/aspire-output/docker-compose.yaml logs --tail=120 webhook mcpserver waha
   ```

   A successful run shows:

   - `AgentForge.WebApi` calling Azure AI Foundry successfully
   - `AgentForge.WebApi` sending `POST http://waha:3000/api/sendText`
   - WAHA returning `201` for `/api/sendText`

If you need WhatsApp media previews or automatic webhook registration outside local Aspire, provide a public `WEBHOOK_BASE_URL` for the deployed `webhook` service.

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
such as `travel-demo.qloop.tech` for the webhook and `travel-waha-demo.qloop.tech` for the WAHA dashboard.

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
WAHAAPIKEY='...'
WAHADASHBOARDPASSWORD='...'
WAHASWAGGERPASSWORD='...'
WAHAWEBHOOKSECRET='...'
MCPSERVER_IMAGE='agentforge-mcpserver-local:deploytest'
WEBHOOK_IMAGE='agentforge-webhook-local:deploytest'
MCPSERVER_PORT='8081'
WEBHOOK_PORT='8080'
WEBHOOK_HOST_PORT='8080'
WAHA_DASHBOARD_HOST_PORT='3000'
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
  `https://<vertical-id>-waha-demo.qloop.tech/dashboard` to WAHA
- starts a managed local `cloudflared` process and stores its PID/log under
  `~/.config/agentforge-demo/`

The public WAHA dashboard remains protected by WAHA's own dashboard login:

- username: `admin`
- password: `WAHADASHBOARDPASSWORD`

By default this workflow is optimized for **one active local demo at a time** because the tunnel config
routes the current demo's webhook and WAHA dashboard hostnames to the current host-published ports.

**Stop the current demo**

```bash
scripts/stop-demo-vertical.sh travel
```

If you omit the vertical ID, the stop script uses the most recent deployment state from
`~/.config/agentforge-demo/current-demo.env`.
