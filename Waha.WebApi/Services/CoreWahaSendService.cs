using System.Text;
using Waha.WebApi.Models;

namespace Waha.WebApi.Services;

/// <summary>
/// WAHA Core (free tier) implementation of <see cref="IWahaSendService"/>.
/// All Plus-only features fall back to rich text equivalents:
/// <list type="bullet">
///   <item><see cref="SendImageAsync"/>: sends the URL as text with <c>linkPreview: true</c>
///   so WhatsApp renders a native link-preview card.</item>
///   <item><see cref="SendListAsync"/>: formats sections as numbered emoji lists.</item>
///   <item><see cref="SendButtonsAsync"/>: formats buttons as numbered text options.</item>
/// </list>
/// </summary>
public sealed class CoreWahaSendService(WahaApiClient wahaClient) : IWahaSendService
{
    public Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
        => wahaClient.SendTextAsync(chatId, text, ct: ct);

    /// <summary>
    /// Sends the image URL as a text message with <c>linkPreview: true</c>.
    /// WhatsApp fetches the URL and renders a native preview card — the best available
    /// experience without WAHA Plus. The caption is prepended if provided.
    /// </summary>
    public async Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default)
    {
        var text = string.IsNullOrWhiteSpace(caption)
            ? imageUrl
            : $"🖼️ *{caption}*\n{imageUrl}";

        await wahaClient.SendTextAsync(chatId, text, linkPreview: true, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Formats an interactive list as a numbered emoji text message since
    /// <c>POST /api/sendList</c> is not available in Core (or NOWEB engine).
    /// </summary>
    public async Task SendListAsync(string chatId, string title, string body, string footer,
        string buttonText, ListSection[] sections, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(title))
            sb.AppendLine($"*{title}*");
        if (!string.IsNullOrWhiteSpace(body))
            sb.AppendLine(body);

        var emojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣", "🔟" };
        int itemIndex = 0;

        foreach (var section in sections)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
                sb.AppendLine($"\n_{section.Title}_");

            foreach (var row in section.Rows)
            {
                var emoji = itemIndex < emojis.Length ? emojis[itemIndex] : $"{itemIndex + 1}.";
                sb.AppendLine(string.IsNullOrWhiteSpace(row.Description)
                    ? $"{emoji} {row.Title}"
                    : $"{emoji} *{row.Title}* — {row.Description}");
                itemIndex++;
            }
        }

        if (!string.IsNullOrWhiteSpace(footer))
            sb.AppendLine($"\n_{footer}_");

        await wahaClient.SendTextAsync(chatId, sb.ToString().Trim(), ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Formats quick-reply buttons as numbered text options since
    /// <c>POST /api/send/buttons/reply</c> is not available in Core (or NOWEB engine).
    /// </summary>
    public async Task SendButtonsAsync(string chatId, string body, ButtonItem[] buttons, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(body))
            sb.AppendLine(body);

        var emojis = new[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣" };
        for (int i = 0; i < buttons.Length; i++)
        {
            var emoji = i < emojis.Length ? emojis[i] : $"{i + 1}.";
            sb.AppendLine($"{emoji} {buttons[i].ButtonText.DisplayText}");
        }

        sb.AppendLine("\n_Reply with the number of your choice._");

        await wahaClient.SendTextAsync(chatId, sb.ToString().Trim(), ct: ct).ConfigureAwait(false);
    }
}
