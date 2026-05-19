namespace Waha.WebApi.Services;

public partial class WebhookRegistrationService
{
    [LoggerMessage(LogLevel.Information, "Registering WAHA webhook at: {WebhookUrl}")]
    partial void LogRegisteringWahaWebhookAtWebhookurl(string webhookUrl);

    [LoggerMessage(LogLevel.Information, "WAHA webhook registered successfully at: {WebhookUrl}")]
    partial void LogWahaWebhookRegisteredSuccessfullyAtWebhookurl(string webhookUrl);

    [LoggerMessage(LogLevel.Warning, "Attempt {Attempt}/5 to register webhook failed. Retrying in {Delay}s...")]
    partial void LogAttemptAttemptToRegisterWebhookFailedRetryingInDelayS(int attempt, double delay, Exception exception);
}