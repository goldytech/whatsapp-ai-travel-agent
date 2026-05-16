using Waha.WebApi.Endpoints;
using Waha.WebApi.Handlers;
using Waha.WebApi.Scheduling;
using Waha.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// ─── WAHA HTTP Client ─────────────────────────────────────────────────────────
builder.Services.AddHttpClient<WahaApiClient>(client =>
{
    client.BaseAddress = new Uri("http://waha");
    var apiKey = builder.Configuration["WAHA_API_KEY"] ?? string.Empty;
    client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
});

// ─── MCP Server HTTP Client (Aspire service discovery) ────────────────────────
builder.Services.AddHttpClient("mcpserver", client =>
{
    client.BaseAddress = new Uri("http://mcpserver");
});

// ─── Azure OpenAI Chat Client ─────────────────────────────────────────────────
builder.AddAzureChatCompletionsClient(connectionName: "ai-foundry")
    .AddChatClient("gpt-5.4-mini");

// ─── AI Agent Services ────────────────────────────────────────────────────────
builder.Services.AddSingleton<McpClientProvider>();
builder.Services.AddSingleton<AgentSessionStore>();
builder.Services.AddSingleton<TravelAgentFactory>();
builder.Services.AddScoped<AgentChatService>();

// ─── Bot Handlers (used by scheduler for reminders/post-trip) ─────────────────
builder.Services.AddScoped<TravelBotHandler>();
builder.Services.AddScoped<FeedbackHandler>();

// ─── Background Services ──────────────────────────────────────────────────────
builder.Services.AddSingleton<SchedulerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<SchedulerService>());
builder.Services.AddSingleton<WebhookRegistrationService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<WebhookRegistrationService>());

// ─── JSON serialisation ───────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapWebhookEndpoints();

app.Run();
