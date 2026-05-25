namespace AgentForge.WebApi.Services;

/// <summary>
/// Registers the public webhook URL with WAHA on startup.
/// Reads the tunnel URL from multiple possible Aspire service discovery env var formats:
///   WEBHOOK_HTTPS            (Aspire DevTunnel simple format — {RESOURCE}_{SCHEME})
///   services__webhook__https__0 (Aspire service discovery format)
///   WEBHOOK_BASE_URL         (manual override via user-secrets or env)
/// Retries in background until the tunnel URL is available.
/// Also exposes RegisterAsync for manual trigger via admin endpoint.
/// </summary>
public sealed partial class WebhookRegistrationService(
    WahaApiClient wahaClient,
    IConfiguration config,
    IHostEnvironment environment,
    ILogger<WebhookRegistrationService> logger) : BackgroundService
{
    private volatile string? _registeredUrl;

    public string? RegisteredUrl => _registeredUrl;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Give WAHA and the tunnel a moment to finish initialising
        await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false);

        while (!ct.IsCancellationRequested)
        {
            var tunnelUrl = ResolveTunnelUrl();

            if (!string.IsNullOrWhiteSpace(tunnelUrl))
            {
                await RegisterAsync(tunnelUrl, ct).ConfigureAwait(false);
                return; // Successfully registered — stop background loop
            }

            var retryMessage = environment.IsDevelopment()
                ? "Tunnel URL not yet available. Will retry in 15s. " +
                  "Once the dev tunnel is healthy, webhook will be registered automatically. " +
                  "You can also POST /admin/register-webhook?url={{tunnelUrl}} to register manually."
                : "Tunnel URL not yet available. Will retry in 15s. " +
                  "Once the public webhook URL becomes available, webhook registration will be retried automatically.";

            logger.LogInformation(retryMessage);

            await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
        }
    }

    /// <summary>Called by the admin endpoint for manual/re-registration.</summary>
    public async Task RegisterAsync(string baseUrl, CancellationToken ct = default)
    {
        var webhookUrl = $"{baseUrl.TrimEnd('/')}/webhook";
        LogRegisteringWahaWebhookAtWebhookurl(webhookUrl);

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                await wahaClient.ConfigureWebhookAsync(webhookUrl, ct).ConfigureAwait(false);
                _registeredUrl = webhookUrl;
                LogWahaWebhookRegisteredSuccessfullyAtWebhookurl(webhookUrl);

                // Ensure the WAHA session is running — it may be STOPPED after a container
                // restart (persistent container keeps config but not session state).
                await wahaClient.EnsureSessionWorkingAsync(ct).ConfigureAwait(false);
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

    private string? ResolveTunnelUrl()
    {
        // Priority order — check all known Aspire env var formats
        return config["WEBHOOK_BASE_URL"]           // manual override
            ?? config["WEBHOOK_HTTPS"]              // Aspire DevTunnel: {RESOURCE}_{SCHEME}
            ?? config["services:webhook:https:0"]   // Aspire service discovery format
            ?? config["services:waha-webhook:https:0"]; // legacy key we tried initially
    }
}
