using AgentForge.AppHost;
using Aspire.Hosting.Docker;
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
        service.Ports.Add("${WAHA_DASHBOARD_HOST_PORT:-3000}:3000");
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
    var composeDashboardBrowserToken = builder.AddParameter("composeDashboardBrowserToken", secret: true);

    builder.AddDockerComposeEnvironment("compose")
        .ConfigureEnvFile(env =>
        {
            SetEnvMetadata(
                env,
                "COMPOSEDASHBOARDBROWSERTOKEN",
                defaultValue: null,
                description: "Browser token used to sign in to the published Aspire dashboard.");
            SetEnvMetadata(
                env,
                "WEBHOOK_BASE_URL",
                settings.ConfiguredWebhookBaseUrl,
                "Public base URL that WAHA should call for webhook delivery in published deployments. " +
                "For local demos, set this to your external tunnel URL before starting Docker Compose.");
            SetEnvMetadata(
                env,
                "WEBHOOK_HOST_PORT",
                "8080",
                "Host port that exposes the published webhook container for direct VPS access or an external tunnel.");
            SetEnvMetadata(
                env,
                "WAHA_DASHBOARD_HOST_PORT",
                "3000",
                "Host port that exposes the published WAHA dashboard for VPS setup, QR scanning, or an external tunnel.");
        })
        .WithDashboard(dashboard => dashboard
            .WithEnvironment("Dashboard__ApplicationName", "AgentForge")
            .WithEnvironment("Dashboard__Frontend__AuthMode", "BrowserToken")
            .WithEnvironment("Dashboard__Frontend__BrowserToken", composeDashboardBrowserToken));
}
#endregion

builder.Build().Run();

static void SetEnvMetadata(
    IDictionary<string, CapturedEnvironmentVariable> env,
    string name,
    string? defaultValue,
    string description)
{
    if (!env.TryGetValue(name, out var variable))
    {
        variable = new CapturedEnvironmentVariable
        {
            Name = name
        };
        env[name] = variable;
    }

    variable.Description = description;

    if (defaultValue is not null)
    {
        variable.DefaultValue = defaultValue;
    }
}