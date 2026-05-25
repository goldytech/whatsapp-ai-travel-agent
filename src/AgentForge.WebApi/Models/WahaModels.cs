namespace AgentForge.WebApi.Models;

// ─── Inbound: WAHA Webhook Events ───────────────────────────────────────────

public record WahaWebhookPayload(
    [property: JsonPropertyName("event")] string Event,
    [property: JsonPropertyName("session")] string Session,
    [property: JsonPropertyName("payload")] JsonElement? Payload
);

public record WahaMessage(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("timestamp")] long Timestamp,
    [property: JsonPropertyName("from")] string From,
    [property: JsonPropertyName("fromMe")] bool FromMe,
    [property: JsonPropertyName("to")] string To,
    [property: JsonPropertyName("body")] string? Body,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("hasMedia")] bool HasMedia,
    [property: JsonPropertyName("_data")] JsonElement? Data
);

public record WahaPollVote(
    [property: JsonPropertyName("msgId")] string MessageId,
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("selectedOptions")] string[] SelectedOptions
);

// ─── Outbound: WAHA Send Models ─────────────────────────────────────────────

public record SendTextRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("linkPreview")] bool? LinkPreview = null,
    [property: JsonPropertyName("session")] string Session = "default"
);

public record SendImageRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("file")] MediaFile File,
    [property: JsonPropertyName("caption")] string? Caption = null,
    [property: JsonPropertyName("session")] string Session = "default"
);

public record SendListRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("footer")] string Footer,
    [property: JsonPropertyName("buttonText")] string ButtonText,
    [property: JsonPropertyName("sections")] ListSection[] Sections,
    [property: JsonPropertyName("session")] string Session = "default"
);

public record ListSection(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("rows")] ListRow[] Rows
);

public record ListRow(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string? Description = null
);

public record SendButtonsRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("body")] string Body,
    [property: JsonPropertyName("buttons")] ButtonItem[] Buttons,
    [property: JsonPropertyName("session")] string Session = "default"
);

public record ButtonItem(
    [property: JsonPropertyName("buttonId")] string ButtonId,
    [property: JsonPropertyName("buttonText")] ButtonText ButtonText
);

public record ButtonText(
    [property: JsonPropertyName("displayText")] string DisplayText
);

public record SendPollRequest(
    [property: JsonPropertyName("chatId")] string ChatId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("options")] PollOption[] Options,
    [property: JsonPropertyName("session")] string Session = "default"
);

public record PollOption(
    [property: JsonPropertyName("name")] string Name
);

public record MediaFile(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("mimetype")] string? MimeType = null
);

// ─── WAHA Session Webhook Config ────────────────────────────────────────────

public record SessionConfigRequest(
    [property: JsonPropertyName("webhooks")] WebhookConfig[] Webhooks
);

public record WebhookConfig(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("events")] string[] Events,
    [property: JsonPropertyName("hmac")] WebhookHmacConfig? Hmac = null
);

public record WebhookHmacConfig(
    [property: JsonPropertyName("key")] string Key
);
