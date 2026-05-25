using AgentForge.WebApi.Endpoints;
using AgentForge.WebApi.Queue;
using AgentForge.WebApi.Scheduling;
using AgentForge.Verticals.Abstractions;
using AgentForge.Verticals.Hosting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);
var verticalPluginLoader = VerticalPluginLoaderFactory.Create(
    builder.Configuration["VERTICAL_PLUGIN_PATH"],
    builder.Configuration["VERTICAL_PLUGIN_ROOT"],
    builder.Configuration["VERTICAL_ID"]);
var verticalPlugin = verticalPluginLoader.Load();

builder.AddServiceDefaults();
builder.Services.AddSingleton<IVerticalPluginLoader>(verticalPluginLoader);
builder.Services.AddSingleton<IVerticalPlugin>(verticalPlugin);
builder.Services.AddSingleton(verticalPlugin.Descriptor);
builder.Services.AddSingleton<IVerticalDescriptor>(sp => sp.GetRequiredService<IVerticalPlugin>().Descriptor);
builder.Services
    .AddOptions<WahaWebhookSecurityOptions>()
    .Configure(options => options.Secret = builder.Configuration["WAHA_WEBHOOK_SECRET"] ?? string.Empty)
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.Secret), "WAHA_WEBHOOK_SECRET is required.")
    .ValidateOnStart();
builder.Services.AddSingleton<WahaWebhookSignatureValidator>();

// ─── WAHA HTTP Client ─────────────────────────────────────────────────────────
// Resilience (retry=0, 5min total, circuit breaker) is set globally in ServiceDefaults.
// Retries are intentionally disabled there to avoid duplicate WhatsApp messages.
builder.Services.AddHttpClient<WahaApiClient>(client =>
{
    client.BaseAddress = new Uri("http://waha");
    var apiKey = builder.Configuration["WAHA_API_KEY"] ?? string.Empty;
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

// ─── MCP Server HTTP Client (Aspire service discovery) ────────────────────────
// MCP StreamableHttp uses short-lived request/response cycles per tool call —
// the 3-min AttemptTimeout from ServiceDefaults is sufficient.
builder.Services.AddHttpClient("mcpserver", client =>
{
    client.BaseAddress = new Uri("http://mcpserver");
});

// ─── Azure OpenAI Chat Client ─────────────────────────────────────────────────
builder.AddAzureChatCompletionsClient(connectionName: "ai-foundry")
    .AddChatClient("gpt-5.4-mini");

// ─── WAHA Send Service (tier-aware strategy pattern) ─────────────────────────
// CoreWahaSendService — free tier: sendImage falls back to text with linkPreview,
//   sendList/sendButtons formatted as numbered text.
// PlusWahaSendService — paid tier: sendImage uses native /api/sendImage;
//   sendList/sendButtons attempt native APIs with text fallback on engine error.
// Active implementation is selected based on WAHA_TIER env var (propagated from AppHost).
builder.Services.AddKeyedSingleton<IWahaSendService, CoreWahaSendService>("Core");
builder.Services.AddSingleton<CoreWahaSendService>(); // needed by PlusWahaSendService as fallback
builder.Services.AddKeyedSingleton<IWahaSendService, PlusWahaSendService>("Plus");
builder.Services.AddSingleton<IWahaSendService>(sp =>
{
    var tierStr = (builder.Configuration["WAHA_TIER"] ?? string.Empty).Trim();
    var key = tierStr.Equals("Plus", StringComparison.OrdinalIgnoreCase) ? "Plus" : "Core";
    if (!string.IsNullOrEmpty(tierStr) && !key.Equals(tierStr, StringComparison.OrdinalIgnoreCase))
    {
        sp.GetRequiredService<ILoggerFactory>()
          .CreateLogger("WahaConfig")
          .LogWarning("Unknown WAHA_TIER value '{Tier}' — defaulting to Core", tierStr);
    }
    return sp.GetRequiredKeyedService<IWahaSendService>(key);
});
builder.Services.AddSingleton<IMessageSender>(sp => sp.GetRequiredService<IWahaSendService>());

builder.Services.AddSingleton<McpClientProvider>();
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddSingleton<IAgentFactory, VerticalAgentFactory>();
builder.Services.AddScoped<AgentChatService>();

// ─── Bot Handlers (used by scheduler for reminders/post-trip) ─────────────────
verticalPlugin.RegisterWebApiServices(builder.Services);

// ─── Background Services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<WhatsAppMessageQueue>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WhatsAppMessageQueue>());
builder.Services.AddSingleton<SchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SchedulerService>());
builder.Services.AddSingleton<WebhookRegistrationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookRegistrationService>());

// ─── JSON serialisation ───────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // Forward only XForwardedProto so Kestrel reports https when behind DevTunnel.
    // XForwardedHost is intentionally omitted — PreviewEndpoint uses WEBHOOK_BASE_URL as the
    // authoritative public host, so a spoofed X-Forwarded-Host header has no effect.
    // KnownIPNetworks/KnownProxies are cleared because Azure DevTunnel uses dynamic IPs.
    // ForwardLimit = 1 prevents header-peeling attacks by accepting exactly one proxy hop.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

var app = builder.Build();

app.UseForwardedHeaders();
app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapWebhookEndpoints();
app.MapPreviewEndpoint();

app.Run();
