namespace AgentForge.WebApi.Services;

public partial class WebhookRegistrationService
{
    [LoggerMessage(LogLevel.Information, "Registering OpenWA webhook at: {WebhookUrl}")]
    partial void LogRegisteringOpenWaWebhookAtWebhookurl(string webhookUrl);

    [LoggerMessage(LogLevel.Information, "OpenWA webhook registered successfully at: {WebhookUrl}")]
    partial void LogOpenWaWebhookRegisteredSuccessfullyAtWebhookurl(string webhookUrl);

    [LoggerMessage(LogLevel.Warning, "Attempt {Attempt}/5 to register webhook failed. Retrying in {Delay}s...")]
    partial void LogAttemptAttemptToRegisterWebhookFailedRetryingInDelayS(int attempt, double delay, Exception exception);

    [LoggerMessage(
        LogLevel.Information,
        "Public webhook URL not yet available from Aspire dev tunnel discovery. Will retry in 15s. " +
        "Once the local tunnel is healthy, OpenWA webhook registration will happen automatically. " +
        "You can also POST /admin/register-webhook?url={{publicUrl}} to register manually.")]
    partial void LogWaitingForAspireTunnelUrl();

    [LoggerMessage(
        LogLevel.Warning,
        "Published deployments do not provision Aspire dev tunnels. Set WEBHOOK_BASE_URL to your public host, reverse-proxy URL, or external tunnel URL and restart the webhook service.")]
    partial void LogPublishedDeploymentRequiresWebhookBaseUrl();
}