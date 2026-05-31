using AgentForge.AppHost;
using Aspire.Hosting.Docker;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);
var settings = AppHostSettings.Load(builder.Configuration, builder.ExecutionContext.IsPublishMode);
var localParameters = AppHostLocalParameters.Create(builder, settings.IsPublishMode);

#region Secrets
var openWaApiKey = builder.AddParameter("openWaApiKey", secret: true);
var openWaEncryptionKey = builder.AddParameter("openWaEncryptionKey", secret: true);
var openWaWebhookSecret = builder.AddParameter("openWaWebhookSecret", secret: true);
var openWaPostgresPassword = builder.AddParameter("openWaPostgresPassword", secret: true);
#endregion

#region Core resources
var openWaRedis = builder.AddRedis("openwa-redis")
    .WithDataVolume();
var openWaPostgres = builder.AddPostgres("openwa-postgres", password: openWaPostgresPassword)
    .WithDataVolume();
var openWaDatabase = openWaPostgres.AddDatabase("openwa-db", "openwa");

var openWa = builder.AddOpenWa("openwa", openWaApiKey, openWaEncryptionKey)
    .WithPostgres(openWaDatabase)
    .WithRedis(openWaRedis)
    .WithDataVolume()
    .WithPersistentLifetime()
    .WaitFor(openWaPostgres)
    .WaitFor(openWaRedis);
var openWaDashboard = builder.AddOpenWaDashboard("openwa-dashboard", openWa)
    .WaitFor(openWa);
if (settings.IsPublishMode)
{
    openWa.PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
    });
    openWaDashboard.PublishAsDockerComposeService((_, service) =>
    {
        service.Restart = "unless-stopped";
        service.Ports.Add("${OPENWA_DASHBOARD_HOST_PORT:-2886}:80");
    });
}

var openWaEndpoint = openWa.Resource.PrimaryEndpoint;

var mcpServer = builder.AddProject<AgentForge_McpHost>("mcpserver")
    .WithLocalVerticalInputs(localParameters)
    .WithPublishVerticalRuntime(settings, "mcpserver");

var aiFoundry = builder.AddConnectionString("ai-foundry");

var webhookApi = builder.AddProject<AgentForge_WebApi>("webhook")
    .WithReference(openWaEndpoint)
    .WithReference(mcpServer)
    .WithReference(aiFoundry)
    .WithEnvironment("OPENWA_API_KEY", openWaApiKey)
    .WithEnvironment("OPENWA_WEBHOOK_SECRET", openWaWebhookSecret)
    .WaitFor(openWa)
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

    var webhookTunnel = builder.AddDevTunnel("openwa-webhook")
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
                "Public base URL that OpenWA should call for webhook delivery in published deployments. " +
                "For local demos, set this to your external tunnel URL before starting Docker Compose.");
            SetEnvMetadata(
                env,
                "WEBHOOK_HOST_PORT",
                "8080",
                "Host port that exposes the published webhook container for direct VPS access or an external tunnel.");
            SetEnvMetadata(
                env,
                "OPENWA_DASHBOARD_HOST_PORT",
                "2886",
                "Host port that exposes the published OpenWA dashboard for VPS setup, QR scanning, or an external tunnel.");
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