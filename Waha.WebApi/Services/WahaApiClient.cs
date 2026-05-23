namespace Waha.WebApi.Services;

public sealed class WahaApiClient(
    HttpClient httpClient,
    ILogger<WahaApiClient> logger)
{
    // ─── Messaging ───────────────────────────────────────────────────────────

    public async Task SendTextAsync(string chatId, string text, bool? linkPreview = null, CancellationToken ct = default)
    {
        var request = new SendTextRequest(chatId, text, linkPreview);
        await PostAsync("/api/sendText", request, ct).ConfigureAwait(false);
    }

    public async Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default)
    {
        var request = new SendImageRequest(chatId, new MediaFile(imageUrl), caption);
        await PostAsync("/api/sendImage", request, ct).ConfigureAwait(false);
    }

    public async Task SendListAsync(string chatId, string title, string body, string footer,
        string buttonText, ListSection[] sections, CancellationToken ct = default)
    {
        var request = new SendListRequest(chatId, title, body, footer, buttonText, sections);
        await PostAsync("/api/sendList", request, ct).ConfigureAwait(false);
    }

    public async Task SendButtonsAsync(string chatId, string body, ButtonItem[] buttons, CancellationToken ct = default)
    {
        var request = new SendButtonsRequest(chatId, body, buttons);
        await PostAsync("/api/sendButtons", request, ct).ConfigureAwait(false);
    }

    public async Task SendPollAsync(string chatId, string question, string[] options, CancellationToken ct = default)
    {
        var pollOptions = options.Select(o => new PollOption(o)).ToArray();
        var request = new SendPollRequest(chatId, question, pollOptions);
        await PostAsync("/api/sendPoll", request, ct).ConfigureAwait(false);
    }

    // ─── Session Management ──────────────────────────────────────────────────

    /// <summary>
    /// Checks session status and starts it if STOPPED or FAILED.
    /// Safe to call when the session is already WORKING — returns immediately.
    /// </summary>
    public async Task EnsureSessionWorkingAsync(CancellationToken ct = default)
    {
        using var response = await httpClient.GetAsync("/api/sessions/default", ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);
        var status = doc.RootElement.GetProperty("status").GetString();

        if (status is "WORKING" or "STARTING")
        {
            logger.LogInformation("WAHA session status: {Status} — no action needed", status);
            return;
        }

        logger.LogWarning("WAHA session is {Status} — starting it now", status);
        using var startResponse = await httpClient.PostAsync(
            "/api/sessions/default/start",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
            ct).ConfigureAwait(false);

        if (startResponse.IsSuccessStatusCode)
            logger.LogInformation("WAHA session started successfully");
        else
        {
            var err = await startResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            logger.LogWarning("Failed to start WAHA session: {Status} {Body}", startResponse.StatusCode, err);
        }
    }

    // ─── Webhook Configuration ───────────────────────────────────────────────

    public async Task ConfigureWebhookAsync(string webhookUrl, CancellationToken ct = default)
    {
        // WAHA free edition uses PUT /api/sessions/default with a "config" wrapper.
        // This restarts the session with the new config, but WhatsApp auth persists
        // because the session volume (/app/.sessions) is mounted.
        var body = new
        {
            config = new SessionConfigRequest(
            [
                new WebhookConfig(webhookUrl, ["message", "session.status", "poll.vote"])
            ])
        };

        var response = await httpClient.PutAsync(
            "/api/sessions/default",
            JsonContent.Create(body),
            ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            logger.LogWarning("Failed to configure WAHA webhook: {Status} {Body}", response.StatusCode, bodyText);
        }
        else
        {
            logger.LogInformation("WAHA webhook configured: {Url}", webhookUrl);
        }
    }

    // ─── Private Helpers ─────────────────────────────────────────────────────

    private async Task PostAsync<T>(string path, T payload, CancellationToken ct)
    {
        var response = await httpClient.PostAsync(path, JsonContent.Create(payload), ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            logger.LogWarning("WAHA API {Path} returned {Status}: {Body}", path, response.StatusCode, body);
        }
    }
}
