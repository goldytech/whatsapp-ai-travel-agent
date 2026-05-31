using AgentForge.WebApi.Queue;
using Microsoft.AspNetCore.Mvc;

namespace AgentForge.WebApi.Endpoints;

public static class WebhookEndpoint
{
    private const int MaxWebhookBodyBytes = 64 * 1024;

    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook", HandleWebhookAsync)
           .AllowAnonymous()
           .WithMetadata(new RequestSizeLimitAttribute(MaxWebhookBodyBytes))
           .WithName("ReceiveWahaWebhook");

        if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
           // Admin endpoint for manual webhook registration — useful when the tunnel URL
           // isn't automatically injected (e.g., first start before tunnel warms up).
           // Usage: POST /admin/register-webhook?url=https://xxx.devtunnels.ms
           app.MapPost("/admin/register-webhook", RegisterWebhookAsync)
              .AllowAnonymous()
              .WithName("RegisterWahaWebhook");
        }

        return app;
    }

    private static async Task<IResult> HandleWebhookAsync(
        HttpRequest request,
        WahaWebhookSignatureValidator signatureValidator,
        WhatsAppMessageQueue messageQueue,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger(nameof(WebhookEndpoint));
        var validation = await signatureValidator.ValidateAsync(request, ct).ConfigureAwait(false);
        if (!validation.IsValid)
           return Results.BadRequest("Missing or invalid webhook signature.");

        WahaWebhookPayload? payload;
        try
        {
           payload = JsonSerializer.Deserialize<WahaWebhookPayload>(validation.BodyBytes!, JsonSerializerOptions.Web);
        }
        catch (JsonException ex)
        {
           logger.LogWarning(ex, "Received WAHA webhook with an invalid JSON payload");
           return Results.BadRequest("Invalid webhook payload.");
        }

        if (payload is null)
           return Results.BadRequest("Webhook payload is required.");

        logger.LogDebug("Received WAHA event: {Event} from session: {Session}", payload.Event, payload.Session);

        if (payload.Event != "message")
           return Results.Ok();

        var message = payload.Payload?.Deserialize<WahaMessage>(JsonSerializerOptions.Web);
        if (message is null || message.FromMe || string.IsNullOrWhiteSpace(message.Body))
            return Results.Ok();

        // Enqueue for background processing — webhook must return 200 quickly.
        // The queue serialises processing, provides backpressure, and respects app shutdown.
        messageQueue.TryEnqueue(message.From, message.Body);

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
