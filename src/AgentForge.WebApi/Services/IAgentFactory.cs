using Microsoft.Agents.AI;

namespace AgentForge.WebApi.Services;

public interface IAgentFactory
{
    Task<ChatClientAgent> GetAgentAsync(CancellationToken ct = default);
}
