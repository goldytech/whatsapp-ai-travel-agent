using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaMessageSender(OpenWaApiClient openWaApiClient) : IMessageSender
{
    public Task SendTextAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
        => openWaApiClient.SendTextAsync(phoneNumber, message, cancellationToken);

    public Task SendImageAsync(
        string phoneNumber,
        string imageUrl,
        string? caption = null,
        CancellationToken cancellationToken = default)
        => openWaApiClient.SendImageAsync(phoneNumber, imageUrl, caption, cancellationToken);
}
