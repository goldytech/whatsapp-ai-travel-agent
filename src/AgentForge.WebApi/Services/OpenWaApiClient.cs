using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentForge.WebApi.Models;
using Microsoft.Extensions.Options;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaApiClient(
    HttpClient httpClient,
    IOptions<OpenWaWebhookSecurityOptions> webhookSecurityOptions,
    ILogger<OpenWaApiClient> logger)
{
    private static readonly string[] WebhookEvents =
    [
        "message.received",
        "session.status"
    ];

    public async Task SendTextAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        await PostAsync(
                "/api/sessions/default/messages/send-text",
                new OpenWaSendTextRequest(phoneNumber, message),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SendImageAsync(
        string phoneNumber,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        await PostAsync(
                "/api/sessions/default/messages/send-image",
                new OpenWaSendImageRequest(phoneNumber, new OpenWaImagePayload(imageUrl), caption),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task EnsureDefaultSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await GetDefaultSessionAsync(cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            await CreateDefaultSessionAsync(cancellationToken).ConfigureAwait(false);
            session = await GetDefaultSessionAsync(cancellationToken).ConfigureAwait(false);
        }

        if (session is null)
        {
            logger.LogWarning("OpenWA default session could not be resolved after creation.");
            return;
        }

        switch (session.Status?.Trim().ToUpperInvariant())
        {
            case "CONNECTED":
            case "CONNECTING":
                return;
            case "SCAN_QR":
            case "INITIALIZING":
                logger.LogInformation("OpenWA default session is waiting for QR onboarding. Status: {Status}", session.Status);
                return;
            case "DISCONNECTED":
            case "FAILED":
            case "STOPPED":
                await RestartDefaultSessionAsync(cancellationToken).ConfigureAwait(false);
                return;
            default:
                logger.LogInformation("OpenWA default session is in status {Status}", session.Status ?? "<unknown>");
                return;
        }
    }

    public async Task ConfigureWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(webhookUrl);

        await EnsureDefaultSessionAsync(cancellationToken).ConfigureAwait(false);

        var existingWebhooks = await GetWebhooksAsync(cancellationToken).ConfigureAwait(false);
        foreach (var existingWebhook in existingWebhooks)
        {
            if (!string.IsNullOrWhiteSpace(existingWebhook.Identifier))
            {
                await DeleteWebhookAsync(existingWebhook.Identifier!, cancellationToken).ConfigureAwait(false);
            }
        }

        var request = new OpenWaWebhookRegistration(
            webhookUrl,
            WebhookEvents,
            webhookSecurityOptions.Value.Secret,
            Enabled: true);

        await PostAsync("/api/sessions/default/webhooks", request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OpenWaSession?> GetDefaultSessionAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/api/sessions/default", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await ReadBodyAsync<OpenWaSession>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateDefaultSessionAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
                "/api/sessions",
                new OpenWaCreateSessionRequest("default", "Default Session"),
                cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task RestartDefaultSessionAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsync("/api/sessions/default/restart", content: null, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<IReadOnlyList<OpenWaWebhookDefinition>> GetWebhooksAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("/api/sessions/default/webhooks", cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        return await ReadBodyAsync<List<OpenWaWebhookDefinition>>(response, cancellationToken).ConfigureAwait(false) ?? [];
    }

    private async Task DeleteWebhookAsync(string webhookId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync(
                $"/api/sessions/default/webhooks/{Uri.EscapeDataString(webhookId)}",
                cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        logger.LogWarning("Failed to delete existing OpenWA webhook {WebhookId}. Status code: {StatusCode}", webhookId, response.StatusCode);
    }

    private async Task PostAsync<TRequest>(string path, TRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<T?> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var bodyText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(bodyText))
        {
            return default;
        }

        using var document = JsonDocument.Parse(bodyText);
        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElement))
        {
            return dataElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                ? default
                : dataElement.Deserialize<T>(JsonSerializerOptions.Web);
        }

        return root.Deserialize<T>(JsonSerializerOptions.Web);
    }
}
