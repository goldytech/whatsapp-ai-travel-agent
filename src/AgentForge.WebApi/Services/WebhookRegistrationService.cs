namespace AgentForge.WebApi.Services;

/// <summary>
/// Registers the public webhook URL with OpenWA on startup.
/// Resolves the public base URL with local Aspire tunnel/service-discovery values first and
/// falls back to WEBHOOK_BASE_URL only when no live tunnel URL is available.
/// Published Docker Compose deployments can opt into configured-only mode, where WEBHOOK_BASE_URL
/// must be supplied explicitly and no tunnel discovery/retry loop is attempted.
/// Also exposes RegisterAsync for manual trigger via admin endpoint.
/// </summary>
public sealed partial class WebhookRegistrationService(
    OpenWaApiClient openWaClient,
    IConfiguration config,
    ILogger<WebhookRegistrationService> logger) : BackgroundService
{
    private const string ConfiguredOnlyPublicUrlMode = "ConfiguredOnly";
    private volatile string? _registeredUrl;

    public string? RegisteredUrl => _registeredUrl;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Give OpenWA and the tunnel a moment to finish initialising
        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

        if (UsesConfiguredPublicUrlOnly())
        {
            var configuredBaseUrl = PublicWebhookUrlResolver.GetConfiguredBaseUrl(config);
            if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                LogPublishedDeploymentRequiresWebhookBaseUrl();
                return;
            }

            await RegisterAsync(configuredBaseUrl, ct).ConfigureAwait(false);
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            var publicBaseUrl = ResolvePublicBaseUrl();

            if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            {
                await RegisterAsync(publicBaseUrl, ct).ConfigureAwait(false);
                return; // Successfully registered — stop background loop
            }

            LogWaitingForAspireTunnelUrl();

            await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Called by the admin endpoint for manual/re-registration.</summary>
    public async Task RegisterAsync(string baseUrl, CancellationToken ct = default)
    {
        var webhookUrl = $"{baseUrl.TrimEnd('/')}/webhook";
        LogRegisteringOpenWaWebhookAtWebhookurl(webhookUrl);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await openWaClient.ConfigureWebhookAsync(webhookUrl, ct).ConfigureAwait(false);
                _registeredUrl = webhookUrl;
                LogOpenWaWebhookRegisteredSuccessfullyAtWebhookurl(webhookUrl);

                // Ensure the OpenWA session is running — it may be STOPPED after a container
                // restart (persistent container keeps config but not session state).
                await openWaClient.EnsureDefaultSessionAsync(ct).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < 5 && !ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)); // 1s, 2s, 4s, 8s, 16s
                LogAttemptAttemptToRegisterWebhookFailedRetryingInDelayS(attempt, delay.TotalSeconds, ex);
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private bool UsesConfiguredPublicUrlOnly()
        => string.Equals(config["WEBHOOK_PUBLIC_URL_MODE"], ConfiguredOnlyPublicUrlMode, StringComparison.OrdinalIgnoreCase);

    private string? ResolvePublicBaseUrl() => PublicWebhookUrlResolver.GetBaseUrl(config);
}
