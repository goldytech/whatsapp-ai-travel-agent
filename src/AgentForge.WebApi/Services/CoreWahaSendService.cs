using System.Text;
using AgentForge.Verticals.Abstractions;
using Microsoft.Extensions.Configuration;
using AgentForge.WebApi.Models;

namespace AgentForge.WebApi.Services;

/// <summary>
/// WAHA Core (free tier) implementation of <see cref="IWahaSendService"/>.
/// All Plus-only features fall back to rich text equivalents:
/// <list type="bullet">
///   <item><see cref="SendImageAsync"/>: wraps the image URL in a <c>/preview</c> page
///   that serves Open Graph meta tags. WhatsApp's link-preview crawler fetches the page
///   and renders a native preview card with the actual image visible in chat.</item>
///   <item><see cref="SendListAsync"/>: formats sections as numbered emoji lists.</item>
///   <item><see cref="SendButtonsAsync"/>: formats buttons as numbered text options.</item>
/// </list>
/// </summary>
public sealed class CoreWahaSendService(
    WahaApiClient wahaClient,
    IConfiguration config,
    IVerticalDescriptor verticalDescriptor) : IWahaSendService
{
    public Task SendTextAsync(string chatId, string text, CancellationToken ct = default)
        => wahaClient.SendTextAsync(chatId, text, ct: ct);

    /// <summary>
    /// Sends the image as a WhatsApp link-preview card by wrapping the image URL
    /// in a <c>/preview</c> HTML page that exposes <c>og:image</c> meta tags.
    /// WhatsApp's crawler fetches the page and renders the image as a preview card.
    /// Falls back to sending the raw URL as text if the base URL is not configured.
    /// </summary>
    public async Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default)
    {
        var previewUrl = BuildPreviewUrl(imageUrl, caption);
        var text = string.IsNullOrWhiteSpace(caption)
            ? previewUrl
            : $"🖼️ *{caption}*\n{previewUrl}";

        await wahaClient.SendTextAsync(chatId, text, linkPreview: true, ct: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds a <c>/preview?imageUrl=...&amp;title=...</c> URL so WhatsApp's OG crawler
    /// can read the image. Falls back to the raw <paramref name="imageUrl"/> when the
    /// public base URL is not yet configured (e.g., first startup before tunnel warms up).
    /// </summary>
    private string BuildPreviewUrl(string imageUrl, string? caption)
    {
        var baseUrl = PublicWebhookUrlResolver.GetBaseUrl(config);

        if (string.IsNullOrWhiteSpace(baseUrl))
            return imageUrl; // tunnel not yet available — degrade gracefully

        var previewImageUrl = ToLocalImagePath(imageUrl, baseUrl) ?? imageUrl;
        var query = $"imageUrl={Uri.EscapeDataString(previewImageUrl)}&title={Uri.EscapeDataString(caption ?? verticalDescriptor.PreviewTitle)}";
        return $"{baseUrl.TrimEnd('/')}/preview?{query}";
    }

    private string? ToLocalImagePath(string imageUrl, string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var baseUri))
            return null;

        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri))
        {
            var relativePath = "/" + imageUrl.TrimStart('/');
            return IsImagePath(relativePath) ? relativePath.TrimStart('/') : null;
        }

        if (!imageUri.Scheme.Equals(baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            || !imageUri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase)
            || imageUri.Port != baseUri.Port
            || !IsImagePath(imageUri.AbsolutePath))
            return null;

        return imageUri.AbsolutePath.TrimStart('/');
    }

    private bool IsImagePath(string path)
        => path.StartsWith(GetNormalizedAssetPathPrefix(), StringComparison.OrdinalIgnoreCase)
            && !path.Contains("/../", StringComparison.Ordinal)
            && !path.EndsWith("/..", StringComparison.Ordinal);

    private string GetNormalizedAssetPathPrefix()
        => "/" + verticalDescriptor.AssetPathPrefix.Trim('/') + "/";

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
