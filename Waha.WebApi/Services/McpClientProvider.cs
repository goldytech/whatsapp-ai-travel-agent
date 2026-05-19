using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Waha.WebApi.Services;

/// <summary>
/// Lazily creates and caches an <see cref="McpClient"/> connected to the Waha.McpServer,
/// then fetches the available MCP tools as <see cref="AITool"/> instances.
/// Uses double-checked locking so the client is created exactly once.
/// </summary>
public sealed class McpClientProvider(
    IHttpClientFactory httpClientFactory,
    ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private McpClient? _mcpClient;
    private IList<McpClientTool>? _tools;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<McpClientProvider> _logger = loggerFactory.CreateLogger<McpClientProvider>();

    public async Task<IList<McpClientTool>> GetToolsAsync(CancellationToken ct = default)
    {
        if (_tools is not null) return _tools;

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_tools is not null) return _tools;

            var httpClient = httpClientFactory.CreateClient("mcpserver");
            var transport = new HttpClientTransport(
                new HttpClientTransportOptions
                {
                    Endpoint = new Uri(httpClient.BaseAddress!, "/mcp"),
                    TransportMode = HttpTransportMode.StreamableHttp
                },
                httpClient,
                loggerFactory,
                ownsHttpClient: false);

            _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: ct).ConfigureAwait(false);
            _tools = await _mcpClient.ListToolsAsync(cancellationToken: ct).ConfigureAwait(false);

            _logger.LogInformation("Connected to MCP server, loaded {Count} tools", _tools.Count);
            return _tools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to MCP server or list tools");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null)
            await _mcpClient.DisposeAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
