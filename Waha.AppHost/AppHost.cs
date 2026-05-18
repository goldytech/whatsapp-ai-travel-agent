using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// ─── WAHA Credentials ─────────────────────────────────────────────────────────
var wahaApiKey = builder.AddParameter("wahaApiKey", secret: true);
var wahaDashboardPassword = builder.AddParameter("wahaDashboardPassword", secret: true);
var wahaSwaggerPassword = builder.AddParameter("wahaSwaggerPassword", secret: true);

// ─── WAHA Container (custom Aspire integration) ───────────────────────────────
var waha = builder.AddWaha("waha", wahaApiKey, wahaDashboardPassword, wahaSwaggerPassword)
    .WithDataVolume()
    .WithPersistentLifetime();

var wahaEndpoint = waha.Resource.PrimaryEndpoint;

// ─── MCP Server ───────────────────────────────────────────────────────────────
// Hosts all 18 Royal Journeys MCP tools (tour search, booking, destination, etc.)
// Starts independently — no dependency on WAHA
var mcpServer = builder.AddProject<Waha_McpServer>("mcpserver");

// ─── Azure AI Foundry connection ──────────────────────────────────────────────
// Connection string stored in user-secrets (already set from previous session):
//   dotnet user-secrets set "ConnectionStrings:ai-foundry" "Endpoint=...;Key=...;DeploymentId=..."
var aiFoundry = builder.AddConnectionString("ai-foundry");

// ─── Webhook WebApi ───────────────────────────────────────────────────────────
// Dependency chain: waha + mcpServer → webhookApi → devTunnel (no circular deps)
var webhookApi = builder.AddProject<Waha_WebApi>("webhook")
    .WithReference(wahaEndpoint)
    .WithReference(mcpServer)
    .WithReference(aiFoundry)
    .WithEnvironment("WAHA_API_KEY", wahaApiKey)
    .WithEnvironment("WEBHOOK_BASE_URL",
        builder.Configuration["WEBHOOK_BASE_URL"] ?? string.Empty)
    .WaitFor(waha)
    .WaitFor(mcpServer);

// ─── Dev Tunnel ───────────────────────────────────────────────────────────────
var _ = builder.AddDevTunnel("waha-webhook")
    .WithReference(webhookApi)
    .WithAnonymousAccess()
    .WaitFor(webhookApi);

builder.Build().Run();