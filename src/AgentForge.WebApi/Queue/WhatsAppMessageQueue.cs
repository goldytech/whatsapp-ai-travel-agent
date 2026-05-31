using System.Threading.Channels;

namespace AgentForge.WebApi.Queue;

/// <summary>
/// Bounded channel-backed queue for incoming WhatsApp messages.
/// The webhook endpoint enqueues messages instantly (fast 200 OK to WAHA),
/// while this background service processes them sequentially, one per-scope.
/// Replaces the previous fire-and-forget <c>Task.Run</c> pattern, providing:
/// - Backpressure (drops oldest when capacity is exceeded)
/// - Proper app-shutdown coordination via <paramref name="stoppingToken"/>
/// - One <see cref="IServiceScope"/> per message so scoped services are resolved correctly
/// </summary>
public sealed class WhatsAppMessageQueue(
    IServiceScopeFactory scopeFactory,
    ILogger<WhatsAppMessageQueue> logger) : BackgroundService
{
    private readonly Channel<(string Phone, string Body)> _channel =
        Channel.CreateBounded<(string Phone, string Body)>(new BoundedChannelOptions(200)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    /// <summary>Enqueues a message for processing. Never throws — drops silently when full.</summary>
    public bool TryEnqueue(string phone, string body)
    {
        if (_channel.Writer.TryWrite((phone, body)))
            return true;

        logger.LogWarning("WhatsAppMessageQueue is full — dropping message from {Phone}", phone);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WhatsAppMessageQueue started");

        await foreach (var (phone, body) in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var agentChat = scope.ServiceProvider.GetRequiredService<AgentChatService>();
                await agentChat.HandleAsync(phone, body, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error handling WhatsApp message from {Phone}", phone);
            }
        }

        logger.LogInformation("WhatsAppMessageQueue stopped");
    }
}
