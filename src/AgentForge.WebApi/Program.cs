using AgentForge.WebApi.Endpoints;
using AgentForge.WebApi.Services;
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
verticalPlugin.ConfigureConfiguration(builder.Configuration);

builder.AddServiceDefaults();
builder.Services.AddSingleton<IVerticalPluginLoader>(verticalPluginLoader);
builder.Services.AddSingleton<IVerticalPlugin>(verticalPlugin);
builder.Services.AddSingleton<IVerticalDescriptor>(sp => sp.GetRequiredService<IVerticalPlugin>().CreateDescriptor(sp));
verticalPlugin.RegisterCommonServices(builder.Services);
builder.Services
    .AddOptions<OpenWaWebhookSecurityOptions>()
    .Configure(options => options.Secret = builder.Configuration["OPENWA_WEBHOOK_SECRET"] ?? string.Empty)
    .ValidateDataAnnotations()
    .Validate(options => !string.IsNullOrWhiteSpace(options.Secret), "OPENWA_WEBHOOK_SECRET is required.")
    .ValidateOnStart();
builder.Services.AddSingleton<OpenWaWebhookSignatureValidator>();
builder.Services.AddSingleton<OpenWaWebhookIdempotencyStore>();

// ─── OpenWA HTTP Client ───────────────────────────────────────────────────────
// Resilience (retry=0, 5min total, circuit breaker) is set globally in ServiceDefaults.
// Retries are intentionally disabled there to avoid duplicate WhatsApp messages.
builder.Services.AddHttpClient<OpenWaApiClient>(client =>
{
    client.BaseAddress = new Uri("http://openwa");
    var apiKey = builder.Configuration["OPENWA_API_KEY"] ?? string.Empty;
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    client.DefaultRequestHeaders.Add("X-API-KEY", apiKey);
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

builder.Services.AddSingleton<IMessageSender, OpenWaMessageSender>();

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
    // XForwardedHost is intentionally omitted — direct OpenWA media URLs use WEBHOOK_BASE_URL as the
    // authoritative public host, so a spoofed X-Forwarded-Host header has no effect.
    // KnownIPNetworks/KnownProxies are cleared because Azure DevTunnel uses dynamic IPs.
    // ForwardLimit = 1 prevents header-peeling attacks by accepting exactly one proxy hop.
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

var app = builder.Build();
_ = app.Services.GetRequiredService<IVerticalDescriptor>();

app.UseForwardedHeaders();
app.MapDefaultEndpoints();
app.MapStaticAssets();
app.MapWebhookEndpoints();

app.Run();
