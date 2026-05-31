using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Services;

/// <summary>
/// Lazily creates and caches the active vertical's <see cref="ChatClientAgent"/> backed by
/// Azure OpenAI and wired to all MCP tools from AgentForge.McpHost.
/// The agent is created on the first request and reused thereafter — MCP tools are fetched
/// once asynchronously, so creation cannot happen in a constructor.
/// </summary>
public sealed class VerticalAgentFactory(
    IChatClient chatClient,
    McpClientProvider mcpProvider,
    IVerticalDescriptor verticalDescriptor,
    ILoggerFactory loggerFactory) : IAgentFactory, IAsyncDisposable
{
    private ChatClientAgent? _agent;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<VerticalAgentFactory> _logger = loggerFactory.CreateLogger<VerticalAgentFactory>();

    public async Task<ChatClientAgent> GetAgentAsync(CancellationToken ct = default)
    {
        if (_agent is not null) return _agent;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_agent is not null) return _agent;

            var mcpTools = await mcpProvider.GetToolsAsync(ct).ConfigureAwait(false);

            _agent = new ChatClientAgent(
                chatClient: chatClient,
                name: verticalDescriptor.AgentName,
                description: verticalDescriptor.AgentDescription,
                instructions: verticalDescriptor.SystemPrompt,
                tools: [.. mcpTools]);

            _logger.LogInformation(
                "{AgentName} agent created with {ToolCount} MCP tools",
                verticalDescriptor.AgentName,
                mcpTools.Count);
            return _agent;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // ChatClientAgent may gain disposal in a future MAF release — check defensively
        if (_agent is object agentObj)
        {
            if (agentObj is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            else if (agentObj is IDisposable disposable)
                disposable.Dispose();
        }

        _lock.Dispose();
    }
}
