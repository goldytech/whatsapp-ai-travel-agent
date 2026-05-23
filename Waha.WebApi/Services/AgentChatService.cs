using Microsoft.Agents.AI;
using System.Text.RegularExpressions;

namespace Waha.WebApi.Services;

/// <summary>
/// Orchestrates the full per-message AI conversation loop:
///   1. Retrieve the customer's serialized session (if any) from <see cref="AgentSessionStore"/>
///   2. Deserialize back into an <see cref="AgentSession"/> (carries full conversation history)
///   3. Run Aria against the incoming message
///   4. Serialize the updated session back to the store
///   5. Dispatch the AI reply via WhatsApp — images first, then text — using <see cref="WahaApiClient"/>
/// </summary>
public sealed partial class AgentChatService(
    TravelAgentFactory agentFactory,
    AgentSessionStore sessionStore,
    WahaApiClient wahaClient,
    ILogger<AgentChatService> logger)
{
    // Matches {{image:https://...}} or {{image:https://...|caption text}}
    [GeneratedRegex(@"\{\{image:(?<url>https?://[^\}|]+?)(?:\|(?<caption>[^\}]*))?\}\}")]
    private static partial Regex ImageMarker();

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
                await wahaClient.SendTextAsync(
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
    /// Parses <c>{{image:URL}}</c> or <c>{{image:URL|caption}}</c> markers embedded by Aria,
    /// sends each image via <see cref="WahaApiClient.SendImageAsync"/>, then sends the
    /// remaining text (markers stripped) via <see cref="WahaApiClient.SendTextAsync"/>.
    /// Image failures are logged at Warning and never block the text reply.
    /// </summary>
    private async Task SendReplyAsync(string phoneNumber, string rawReply, CancellationToken ct)
    {
        var matches = ImageMarker().Matches(rawReply);

        foreach (Match match in matches)
        {
            var url = match.Groups["url"].Value.Trim();
            var caption = match.Groups["caption"].Success ? match.Groups["caption"].Value.Trim() : null;

            try
            {
                await wahaClient.SendImageAsync(phoneNumber, url, caption, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send image to {Phone} — URL: {Url}", phoneNumber, url);
            }
        }

        var textReply = ImageMarker().Replace(rawReply, string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(textReply))
            await wahaClient.SendTextAsync(phoneNumber, textReply, ct).ConfigureAwait(false);
    }
}
