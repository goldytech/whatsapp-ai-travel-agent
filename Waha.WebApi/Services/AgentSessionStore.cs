using System.Collections.Concurrent;
using Microsoft.Agents.AI;

namespace Waha.WebApi.Services;

/// <summary>
/// In-memory store for <see cref="AgentSession"/> objects, keyed by WhatsApp phone number.
/// Preserves full conversation history across webhook invocations for each customer.
/// Sessions persist for the lifetime of the application process.
/// Future: swap implementation to serialize and store in Redis/Cosmos for cross-instance sharing.
/// </summary>
public sealed class AgentSessionStore
{
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    public AgentSession? TryGet(string phoneNumber) =>
        _sessions.TryGetValue(phoneNumber, out var session) ? session : null;

    public void Set(string phoneNumber, AgentSession session) =>
        _sessions[phoneNumber] = session;

    public void Remove(string phoneNumber) =>
        _sessions.TryRemove(phoneNumber, out _);
}
