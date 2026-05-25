using Waha.WebApi.Models;

namespace Waha.WebApi.Services;

/// <summary>
/// WAHA Plus (paid tier) implementation of <see cref="IWahaSendService"/>.
/// Uses native WAHA Plus APIs where available.
/// <list type="bullet">
///   <item><see cref="SendImageAsync"/>: calls <c>POST /api/sendImage</c> — native image bubble.
///   Works with NOWEB engine on Plus.</item>
///   <item><see cref="SendListAsync"/>: calls <c>POST /api/sendList</c> — native WhatsApp list.
///   Only works with WEBJS/WPP engine; falls back to text if the API returns an error (NOWEB).</item>
///   <item><see cref="SendButtonsAsync"/>: calls <c>POST /api/sendButtons</c>.
///   Only works with WEBJS engine; falls back to text on error (NOWEB).</item>
/// </list>
/// </summary>
public sealed class PlusWahaSendService(
    WahaApiClient wahaClient,
    CoreWahaSendService coreFallback,
    ILogger<PlusWahaSendService> logger) : IWahaSendService
{
    public Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
        => wahaClient.SendTextAsync(chatId, text, ct: ct);

    /// <summary>
    /// Sends a native image bubble via <c>POST /api/sendImage</c>.
    /// Available on NOWEB + Plus.
    /// </summary>
    public Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default)
        => wahaClient.SendImageAsync(chatId, imageUrl, caption, ct);

    /// <summary>
    /// Attempts to send a native WhatsApp interactive list via <c>POST /api/sendList</c>.
    /// Note: <c>sendList</c> is only supported on WEBJS/WPP engines. If the API returns an
    /// error (e.g., NOWEB engine), falls back to <see cref="CoreWahaSendService.SendListAsync"/>.
    /// </summary>
    public async Task SendListAsync(string chatId, string title, string body, string footer,
        string buttonText, ListSection[] sections, CancellationToken ct = default)
    {
        try
        {
            await wahaClient.SendListAsync(chatId, title, body, footer, buttonText, sections, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "sendList native API failed (engine may not support it) — falling back to text for {ChatId}", chatId);
            await coreFallback.SendListAsync(chatId, title, body, footer, buttonText, sections, ct)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Attempts to send native quick-reply buttons via <c>POST /api/send/buttons/reply</c>.
    /// Note: buttons are only supported on WEBJS engine. If the API returns an error
    /// (e.g., NOWEB engine), falls back to <see cref="CoreWahaSendService.SendButtonsAsync"/>.
    /// </summary>
    public async Task SendButtonsAsync(string chatId, string body, ButtonItem[] buttons, CancellationToken ct = default)
    {
        try
        {
            await wahaClient.SendButtonsAsync(chatId, body, buttons, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "sendButtons native API failed (engine may not support it) — falling back to text for {ChatId}", chatId);
            await coreFallback.SendButtonsAsync(chatId, body, buttons, ct).ConfigureAwait(false);
        }
    }
}
