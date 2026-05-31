using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentForge.WebApi.Models;

public sealed record OpenWaApiEnvelope<T>(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] T? Data,
    [property: JsonPropertyName("error")] OpenWaApiError? Error);

public sealed record OpenWaApiError(
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("code")] string? Code);

public sealed record OpenWaCreateSessionRequest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record OpenWaSendTextRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("text")] string Text);

public sealed record OpenWaSendImageRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("image")] OpenWaImagePayload Image,
    [property: JsonPropertyName("caption")] string? Caption);

public sealed record OpenWaImagePayload(
    [property: JsonPropertyName("url")] string Url);

public sealed record OpenWaWebhookRegistration(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("events")] string[] Events,
    [property: JsonPropertyName("secret")] string Secret,
    [property: JsonPropertyName("enabled")] bool Enabled);

public sealed record OpenWaWebhookDefinition(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("_id")] string? AlternateId,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("enabled")] bool Enabled)
{
    public string? Identifier => string.IsNullOrWhiteSpace(Id) ? AlternateId : Id;
}

public sealed record OpenWaSession(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("name")] string? Name);

public sealed record OpenWaWebhookPayload(
    [property: JsonPropertyName("event")] string? Event,
    [property: JsonPropertyName("session")] string? Session,
    [property: JsonPropertyName("sessionId")] string? SessionId,
    [property: JsonPropertyName("deliveryId")] string? DeliveryId,
    [property: JsonPropertyName("idempotencyKey")] string? IdempotencyKey,
    [property: JsonPropertyName("data")] JsonElement? Data,
    [property: JsonPropertyName("payload")] JsonElement? Payload)
{
    public string? EffectiveSession => string.IsNullOrWhiteSpace(SessionId) ? Session : SessionId;

    public JsonElement? EventPayload => Data ?? Payload;

    public string? GetDedupeKey()
    {
        if (!string.IsNullOrWhiteSpace(IdempotencyKey))
        {
            return IdempotencyKey;
        }

        if (!string.IsNullOrWhiteSpace(DeliveryId))
        {
            return DeliveryId;
        }

        if (EventPayload is { ValueKind: JsonValueKind.Object } payload
            && payload.TryGetProperty("id", out var idProperty)
            && idProperty.ValueKind == JsonValueKind.String)
        {
            return idProperty.GetString();
        }

        return null;
    }
}

public sealed record OpenWaMessage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("chatId")] string? ChatId,
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("sender")] string? Sender,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("text")] JsonElement? Text,
    [property: JsonPropertyName("fromMe")] bool? FromMe,
    [property: JsonPropertyName("hasMedia")] bool? HasMedia,
    [property: JsonPropertyName("type")] string? Type)
{
    public string? GetSender()
        => !string.IsNullOrWhiteSpace(From) ? From
            : !string.IsNullOrWhiteSpace(Sender) ? Sender
            : ChatId;

    public string? GetBody()
    {
        if (!string.IsNullOrWhiteSpace(Body))
        {
            return Body;
        }

        if (!string.IsNullOrWhiteSpace(Content))
        {
            return Content;
        }

        if (Text is { ValueKind: JsonValueKind.String } textValue)
        {
            return textValue.GetString();
        }

        if (Text is { ValueKind: JsonValueKind.Object } textObject
            && textObject.TryGetProperty("body", out var nestedBody)
            && nestedBody.ValueKind == JsonValueKind.String)
        {
            return nestedBody.GetString();
        }

        return null;
    }
}
