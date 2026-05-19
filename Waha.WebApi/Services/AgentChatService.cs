using Microsoft.Agents.AI;

namespace Waha.WebApi.Services;

/// <summary>
/// Orchestrates the full per-message AI conversation loop:
///   1. Retrieve the customer's serialized session (if any) from <see cref="AgentSessionStore"/>
///   2. Deserialize back into an <see cref="AgentSession"/> (carries full conversation history)
///   3. Run Aria against the incoming message
///   4. Serialize the updated session back to the store
///   5. Send the AI reply via WhatsApp using <see cref="WahaApiClient"/>
/// </summary>
public sealed class AgentChatService(
    TravelAgentFactory agentFactory,
    AgentSessionStore sessionStore,
    WahaApiClient wahaClient,
    ILogger<AgentChatService> logger)
{
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
            var replyText = response.Text ?? "I'm sorry, I couldn't process that. Please try again. 🙏";

            // Persist the updated session for this customer
            sessionStore.Set(phoneNumber, session);

            // Send the AI reply via WhatsApp
            await wahaClient.SendTextAsync(phoneNumber, replyText, ct).ConfigureAwait(false);
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
}
