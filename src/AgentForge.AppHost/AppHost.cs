using AgentForge.AppHost;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
var settings = AppHostSettings.Load(builder.Configuration, builder.ExecutionContext.IsPublishMode);
var localParameters = AppHostLocalParameters.Create(builder, settings.IsPublishMode);

#region Secrets
var wahaApiKey = builder.AddParameter("wahaApiKey", secret: true);
var wahaDashboardPassword = builder.AddParameter("wahaDashboardPassword", secret: true);
var wahaSwaggerPassword = builder.AddParameter("wahaSwaggerPassword", secret: true);
var wahaWebhookSecret = builder.AddParameter("wahaWebhookSecret", secret: true);
#endregion

#region Core resources
var waha = builder.AddWaha("waha", wahaApiKey, wahaDashboardPassword, wahaSwaggerPassword,
        engine: WahaEngine.NOWEB,
        tier: settings.WahaTier)
    .WithDataVolume()
    .WithPersistentLifetime();
if (settings.IsPublishMode)
{
    waha.PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
    });
}

var wahaEndpoint = waha.Resource.PrimaryEndpoint;

var mcpServer = builder.AddProject<AgentForge_McpHost>("mcpserver")
    .WithLocalVerticalInputs(localParameters)
    .WithPublishVerticalRuntime(settings, "mcpserver");

var aiFoundry = builder.AddConnectionString("ai-foundry");

var webhookApi = builder.AddProject<AgentForge_WebApi>("webhook")
    .WithReference(wahaEndpoint)
    .WithReference(mcpServer)
    .WithReference(aiFoundry)
    .WithEnvironment("WAHA_API_KEY", wahaApiKey)
    .WithEnvironment("WAHA_WEBHOOK_SECRET", wahaWebhookSecret)
    .WithEnvironment("WAHA_TIER", settings.WahaTier.ToString())
    .WaitFor(waha)
    .WaitFor(mcpServer)
    .WithLocalVerticalInputs(localParameters);
if (settings.IsPublishMode && !string.IsNullOrWhiteSpace(settings.ConfiguredWebhookBaseUrl))
{
    webhookApi.WithEnvironment("WEBHOOK_BASE_URL", settings.ConfiguredWebhookBaseUrl);
}
webhookApi.WithPublishVerticalRuntime(settings, "webhook");
#endregion

#region Local-only tooling
if (!settings.IsPublishMode)
{
    builder.AddMcpInspector("mcp-inspector", options =>
    {
        options.InspectorVersion = "0.17.5";
    })
        .WithMcpServer(mcpServer);

    var webhookTunnel = builder.AddDevTunnel("waha-webhook")
        .WithReference(webhookApi)
        .WithAnonymousAccess();

    webhookApi.WithReference(webhookApi, webhookTunnel);
}
#endregion

#region Publish-only resources
if (settings.IsPublishMode)
{
    builder.AddDockerComposeEnvironment("compose");
}
#endregion

builder.Build().Run();