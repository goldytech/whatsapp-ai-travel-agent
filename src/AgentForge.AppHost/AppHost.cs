using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
var isPublishMode = builder.ExecutionContext.IsPublishMode;
var verticalId = builder.Configuration["VERTICAL_ID"] ?? "travel";
var verticalPluginRoot = builder.Configuration["VERTICAL_PLUGIN_ROOT"] ?? "/app/plugins";
var verticalPluginSourcePath = builder.Configuration["VERTICAL_PLUGIN_SOURCE_PATH"]
    ?? $"../../artifacts/plugins/{verticalId}";
var verticalPluginMountPath = $"{verticalPluginRoot.TrimEnd('/')}/{verticalId}";

// ─── WAHA Credentials ─────────────────────────────────────────────────────────
var wahaApiKey = builder.AddParameter("wahaApiKey", secret: true);
var wahaDashboardPassword = builder.AddParameter("wahaDashboardPassword", secret: true);
var wahaSwaggerPassword = builder.AddParameter("wahaSwaggerPassword", secret: true);
var wahaWebhookSecret = builder.AddParameter("wahaWebhookSecret", secret: true);

// ─── WAHA Container (custom Aspire integration) ───────────────────────────────
// NOWEB engine: WebSocket-based, no Chromium browser, lightweight (~50MB RAM).
// sendImage is available with WAHA Plus; sendList/sendButtons require WEBJS engine (not used here).
// Tier: read from config ("Core" = free, "Plus" = paid). Defaults to Core.
// To enable Plus: dotnet user-secrets set "WahaTier" "Plus" --project AgentForge.AppHost
var wahaTier = builder.Configuration["WahaTier"] is "Plus" ? WahaTier.Plus : WahaTier.Core;

var waha = builder.AddWaha("waha", wahaApiKey, wahaDashboardPassword, wahaSwaggerPassword,
        engine: WahaEngine.NOWEB,
        tier: wahaTier)
    .WithDataVolume()
    .WithPersistentLifetime();
if (isPublishMode)
{
    waha.PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
    });
}

var wahaEndpoint = waha.Resource.PrimaryEndpoint;

// ─── MCP Server ───────────────────────────────────────────────────────────────
// Hosts all 18 Royal Journeys MCP tools (tour search, booking, destination, etc.)
// Starts independently — no dependency on WAHA
var mcpServer = builder.AddProject<AgentForge_McpHost>("mcpserver");
if (isPublishMode)
{
    mcpServer
        .WithEnvironment("VERTICAL_ID", verticalId)
        .WithEnvironment("VERTICAL_PLUGIN_ROOT", verticalPluginRoot)
        .WithEnvironment("VERTICAL_PLUGIN_PATH", verticalPluginMountPath)
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = "unless-stopped";
            service.AddVolume(new Volume
            {
                Name = $"{verticalId}-plugin-mcpserver",
                Type = "bind",
                Source = verticalPluginSourcePath,
                Target = verticalPluginMountPath,
                ReadOnly = true
            });
        });
}

// ─── Azure AI Foundry connection ──────────────────────────────────────────────
// Connection string stored in user-secrets (already set from previous session):
//   dotnet user-secrets set "ConnectionStrings:ai-foundry" "Endpoint=...;Key=...;DeploymentId=..."
var aiFoundry = builder.AddConnectionString("ai-foundry");

// ─── Webhook WebApi ───────────────────────────────────────────────────────────
// Dependency chain: waha + mcpServer → webhookApi → devTunnel (no circular deps)
var webhookApi = builder.AddProject<AgentForge_WebApi>("webhook")
    .WithReference(wahaEndpoint)
    .WithReference(mcpServer)
    .WithReference(aiFoundry)
    .WithEnvironment("WAHA_API_KEY", wahaApiKey)
    .WithEnvironment("WAHA_WEBHOOK_SECRET", wahaWebhookSecret)
    .WithEnvironment("WAHA_TIER", wahaTier.ToString())
    .WithEnvironment("WEBHOOK_BASE_URL",
        builder.Configuration["WEBHOOK_BASE_URL"] ?? string.Empty)
    .WaitFor(waha)
    .WaitFor(mcpServer);
if (isPublishMode)
{
    webhookApi
        .WithEnvironment("VERTICAL_ID", verticalId)
        .WithEnvironment("VERTICAL_PLUGIN_ROOT", verticalPluginRoot)
        .WithEnvironment("VERTICAL_PLUGIN_PATH", verticalPluginMountPath)
        .PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = "unless-stopped";
            service.AddVolume(new Volume
            {
                Name = $"{verticalId}-plugin-webhook",
                Type = "bind",
                Source = verticalPluginSourcePath,
                Target = verticalPluginMountPath,
                ReadOnly = true
            });
        });
}

// ─── MCP Inspector ────────────────────────────────────────────────────────────
// Browse and test all 18 MCP tools interactively from the Aspire Dashboard.
// Defaults: StreamableHttp transport, path "/mcp" — both match our McpHost setup.
// InspectorVersion: 0.17.2 (bundled default) crashes on Node.js v24 with
// ERR_INVALID_STATE when its createWebReadableStream closes an already-closed
// ReadableStream controller. Fixed in 0.17.5+.
if (!isPublishMode)
{
    builder.AddMcpInspector("mcp-inspector", options =>
    {
        options.InspectorVersion = "0.17.5";
    })
        .WithMcpServer(mcpServer);
}

// ─── Dev Tunnel ───────────────────────────────────────────────────────────────
if (!isPublishMode)
{
    var _ = builder.AddDevTunnel("waha-webhook")
        .WithReference(webhookApi)
        .WithAnonymousAccess()
        .WaitFor(webhookApi);
}

if (isPublishMode)
{
    builder.AddDockerComposeEnvironment("compose");
}

builder.Build().Run();