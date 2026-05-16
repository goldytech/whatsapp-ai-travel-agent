namespace Waha.WebApi.Endpoints;

public static class WebhookEndpoint
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook", HandleWebhookAsync)
           .AllowAnonymous()
           .WithName("ReceiveWahaWebhook");

        // Admin endpoint for manual webhook registration — useful when the tunnel URL
        // isn't automatically injected (e.g., first start before tunnel warms up).
        // Usage: POST /admin/register-webhook?url=https://xxx.devtunnels.ms
        app.MapPost("/admin/register-webhook", RegisterWebhookAsync)
           .WithName("RegisterWahaWebhook");

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        WahaWebhookPayload payload,
        AgentChatService agentChat,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(WebhookEndpoint));
        logger.LogDebug("Received WAHA event: {Event} from session: {Session}", payload.Event, payload.Session);

        if (payload.Event != "message")
            return Results.Ok();

        var message = payload.Payload?.Deserialize<WahaMessage>(JsonSerializerOptions.Web);
        if (message is null || message.FromMe || string.IsNullOrWhiteSpace(message.Body))
            return Results.Ok();

        // Fire-and-forget with error isolation — webhook must return 200 quickly.
        // Use CancellationToken.None so outbound calls aren't cancelled when the
        // inbound HTTP request completes.
        _ = Task.Run(async () =>
        {
            try
            {
                await agentChat.HandleAsync(message.From, message.Body, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling WAHA message from {From}", message.From);
            }
        }, CancellationToken.None);

        return Results.Ok();
    }

    private static async Task<IResult> RegisterWebhookAsync(
        string url,
        WebhookRegistrationService registrationService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Results.BadRequest("url query parameter is required");

        await registrationService.RegisterAsync(url, ct).ConfigureAwait(false);
        return Results.Ok(new { registered = registrationService.RegisteredUrl });
    }
}
