using Microsoft.Agents.AI;
using System.Text.RegularExpressions;
using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Services;

/// <summary>
/// Orchestrates the full per-message AI conversation loop:
///   1. Retrieve the customer's serialized session (if any) from <see cref="AgentSessionStore"/>
///   2. Deserialize back into an <see cref="AgentSession"/> (carries full conversation history)
///   3. Run Aria against the incoming message
///   4. Serialize the updated session back to the store
///   5. Dispatch the AI reply via WhatsApp — images first, then text — using <see cref="IMessageSender"/>
/// </summary>
public sealed partial class AgentChatService(
    IAgentFactory agentFactory,
    AgentSessionStore sessionStore,
    IMessageSender sendService,
    IVerticalDescriptor verticalDescriptor,
    IConfiguration config,
    ILogger<AgentChatService> logger)
{
    // Matches {{image:URL}} or {{image:URL|caption}} where URL is absolute (https?://) or
    // relative under the active vertical's asset path prefix.
    [GeneratedRegex(@"\{\{image:(?<url>https?://[^\}|]+?|[^/\}][^\}|]*?)(?:\|(?<caption>[^\}]*))?\}\}")]
    private static partial Regex ImageMarker();

    /// <summary>
    /// Returns true when <paramref name="path"/> is a safe local image path:
    /// must be under the active vertical's asset prefix, with no traversal or shell characters.
    /// </summary>
    private bool IsLocalImagePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.TrimStart('/').StartsWith(GetNormalizedAssetPathPrefix().TrimStart('/'), StringComparison.OrdinalIgnoreCase)
        && !path.Contains("..")
        && !path.Contains("//")
        && path.IndexOfAny(['\\', ':', '<', '>', '|', '"', '?', '*']) < 0;

    /// <summary>
    /// Returns true when <paramref name="url"/> is an absolute URL whose origin matches
    /// the configured public base and whose path is a safe local image path.
    /// When no base URL is configured the check is skipped (returns true).
    /// </summary>
    private bool IsSameOriginImageUrl(string url)
    {
        var publicBase = PublicWebhookUrlResolver.GetBaseUrl(config);

        if (publicBase is null) return true; // cannot validate without a known base

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!Uri.TryCreate(publicBase.TrimEnd('/'), UriKind.Absolute, out var baseUri)) return false;

        return string.Equals(uri.Scheme, baseUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(uri.Host, baseUri.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == baseUri.Port
            && IsLocalImagePath(uri.AbsolutePath);
    }

    public async Task HandleAsync(string phoneNumber, string userMessage, CancellationToken ct = default)
    {
        try
        {
            var agent = await agentFactory.GetAgentAsync(ct).ConfigureAwait(false);

            // Restore or create a session with CLIENT-managed history.
            // Do NOT pass phoneNumber as conversationId — that overload uses server-managed
            // history which requires the AI service to maintain conversation state, and
            // Azure AI Foundry chat completions does not support that.
            var session = sessionStore.TryGet(phoneNumber)
                ?? await agent.CreateSessionAsync(ct).ConfigureAwait(false);

            // Run the agent
            var response = await agent.RunAsync(userMessage, session, cancellationToken: ct).ConfigureAwait(false);
            var rawReply = response.Text ?? "I'm sorry, I couldn't process that. Please try again. 🙏";

            // Persist the updated session for this customer
            sessionStore.Set(phoneNumber, session);

            // Dispatch images first, then the text reply (markers stripped from text)
            await SendReplyAsync(phoneNumber, rawReply, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AgentChatService error for {Phone}: {Message}", phoneNumber, userMessage);

            // Fallback: send a graceful error message to the customer
            try
            {
                await sendService.SendTextAsync(
                    phoneNumber,
                    "Apologies, I'm having trouble right now 😔 Please try again in a moment, or call us at +91-99999-99999.",
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception fallbackEx)
            {
                // Swallow — don't let fallback errors propagate, but log for diagnostics
                logger.LogDebug(fallbackEx, "Failed to send fallback error message to {Phone}", phoneNumber);
            }
        }
    }

    /// <summary>
    /// Parses <c>{{image:URL}}</c> or <c>{{image:URL|caption}}</c> markers embedded by Aria.
    /// Relative URLs (e.g. <c>images/tours/goa/1.jpg</c>) are expanded to absolute using
    /// <c>WEBHOOK_BASE_URL</c> so OpenWA can fetch them over the public endpoint.
    /// Images are sent sequentially with a 750 ms gap to preserve display order and avoid
    /// out-of-order media delivery when the provider uploads assets concurrently.
    /// Image failures are logged at Warning and never block the text reply.
    /// </summary>
    private async Task SendReplyAsync(string phoneNumber, string rawReply, CancellationToken ct)
    {
        var matches = ImageMarker().Matches(rawReply);

        foreach (Match match in matches)
        {
            var url = match.Groups["url"].Value.Trim();
            var caption = match.Groups["caption"].Success ? match.Groups["caption"].Value.Trim() : null;

            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                // Validate relative path before expanding (prevent traversal / non-image paths)
                if (!IsLocalImagePath(url))
                {
                    logger.LogWarning("Blocked unsafe image path in agent output: {Path}", url);
                    continue;
                }

                var baseUrl = PublicWebhookUrlResolver.GetBaseUrl(config) ?? string.Empty;

                if (string.IsNullOrEmpty(baseUrl))
                {
                    logger.LogWarning("Skipping image — public webhook URL not configured: {Path}", url);
                    continue;
                }

                url = $"{baseUrl.TrimEnd('/')}/{url.TrimStart('/')}";
            }
            else
            {
                // Absolute URL — validate same-origin + safe path to prevent SSRF / prompt injection
                if (!IsSameOriginImageUrl(url))
                {
                    logger.LogWarning("Blocked external or unsafe image URL in agent output (possible prompt injection): {Url}", url);
                    continue;
                }
            }

            try
            {
                await sendService.SendImageAsync(phoneNumber, url, caption, ct).ConfigureAwait(false);
                await Task.Delay(750, ct).ConfigureAwait(false); // preserve order; avoid WhatsApp spam detection
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send image to {Phone} — URL: {Url}", phoneNumber, url);
            }
        }

        var textReply = ImageMarker().Replace(rawReply, string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(textReply))
            await sendService.SendTextAsync(phoneNumber, textReply, ct).ConfigureAwait(false);
    }

    private string GetNormalizedAssetPathPrefix()
        => "/" + verticalDescriptor.AssetPathPrefix.Trim('/') + "/";
}
