using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Waha.WebApi.Constants;

namespace Waha.WebApi.Services;

/// <summary>
/// Lazily creates and caches a <see cref="ChatClientAgent"/> (Aria) backed by Azure OpenAI
/// and wired to all MCP tools from Waha.McpServer.
/// The agent is created on the first request and reused thereafter — MCP tools are fetched
/// once asynchronously, so creation cannot happen in a constructor.
/// </summary>
public sealed class TravelAgentFactory(
    IChatClient chatClient,
    McpClientProvider mcpProvider,
    ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private ChatClientAgent? _agent;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<TravelAgentFactory> _logger = loggerFactory.CreateLogger<TravelAgentFactory>();

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
                name: "Aria",
                description: "Royal Journeys AI Travel Consultant",
                instructions: SystemPrompts.Aria,
                tools: [.. mcpTools]);

            _logger.LogInformation("Aria agent created with {ToolCount} MCP tools", mcpTools.Count);
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
