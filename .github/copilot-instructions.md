# Copilot Instructions — Royal Journeys WhatsApp AI Travel Agent

## Build & Run

```bash
# Build entire solution (zero warnings expected)
dotnet build

# Start all services (Aspire orchestrates WAHA container, McpServer, WebApi, DevTunnel)
aspire start

# Run a single project
dotnet run --project Waha.McpServer
dotnet run --project Waha.WebApi
```

There are no automated test projects in this repository. Verification is done by running the app and exercising the webhook manually (see `Waha.WebApi/Waha.WebApi.http`).

## Architecture Overview

This is a **.NET Aspire** solution with four projects:

| Project | Role |
|---|---|
| `Waha.AppHost` | Aspire orchestrator — wires up all services, secrets, and the DevTunnel |
| `Waha.Hosting` | Custom Aspire integration (`AddWaha`) that encapsulates the WAHA Docker container |
| `Waha.McpServer` | MCP tool server — 18 AI tools and 3 resources, exposed over `StreamableHttp` at `/mcp` |
| `Waha.WebApi` | AI gateway — receives WhatsApp webhooks, runs the Aria agent, sends replies |
| `Waha.ServiceDefaults` | Shared defaults — OpenTelemetry, health checks, HTTP resilience, service discovery |

### Message flow

```
WhatsApp → WAHA container → DevTunnel → /webhook (WebApi)
    → WhatsAppMessageQueue (Channel<T>)
    → AgentChatService → TravelAgentFactory (Aria / ChatClientAgent)
        → Waha.McpServer (MCP tools over StreamableHttp)
        → Azure AI Foundry (GPT-5.4 mini)
    → WahaApiClient → WAHA → WhatsApp
```

**Key design decisions:**
- The `WhatsAppMessageQueue` is a bounded `Channel<T>` (capacity 200, `DropOldest`). The webhook returns `200 OK` immediately and processing happens asynchronously in a `BackgroundService`.
- Conversation history is **client-managed** (`AgentSessionStore`, keyed by phone number). The Azure AI Foundry chat completions API does not support server-managed history — do not use the `conversationId` overload of `CreateSessionAsync`.
- **Retries are intentionally disabled** on all HTTP clients (via `ServiceDefaults`). Retrying `WahaApiClient.SendTextAsync` would deliver the same WhatsApp message multiple times.
- `TravelAgentFactory` uses double-checked locking (`SemaphoreSlim`) to lazily initialise the `ChatClientAgent` (Aria) exactly once — MCP tool discovery is async and cannot happen in a constructor.

## Key Conventions

### C# style
- **Primary constructors** on all services (no `private readonly` field boilerplate for injected deps).
- **`ConfigureAwait(false)`** on every `await` call inside services and library code.
- **C# 13 features** where appropriate: `System.Threading.Lock`, collection expressions (`[.. items]`), etc.
- No `#pragma warning disable` — fix the root cause.

### DI lifetime rules
- Stateful classes → `Singleton`
- Per-request / per-message work (e.g., `AgentChatService`) → `Scoped` (resolved per `IServiceScope` inside the queue's `BackgroundService`)
- Never `Transient` for stateful classes

### HTTP clients
- Always registered via `IHttpClientFactory` — never `new HttpClient()`.
- Service-discovery names match Aspire resource names: `"http://waha"`, `"http://mcpserver"`.

### MCP tool authoring (in `Waha.McpServer/Tools/`)
- Decorate the class with `[McpServerToolType]` and each method with `[McpServerTool]`.
- All parameters and descriptions use `[Description("...")]` attributes.
- Tools are auto-registered via `WithToolsFromAssembly` — no registration needed in `Waha.WebApi`.
- Return plain strings (WhatsApp-friendly, use `*bold*` and emoji). Use raw string literals (`"""..."""`) for multi-line responses.
- Read-only tools get `ReadOnly = true` on `[McpServerTool]`.

### Background work
- Use `Channel<T>` or `IHostedService` — never `_ = Task.Run(...)`.
- Register background services as singletons and add them with `AddHostedService(sp => sp.GetRequiredService<T>())` so the same instance is both injectable and hosted.

### Secrets & configuration
- All secrets live in **`Waha.AppHost` user secrets** (`dotnet user-secrets` in that project).
- The `ai-foundry` connection string format: `Endpoint=https://...;Key=...` (optional `DeploymentId=...`).
- `WEBHOOK_BASE_URL` env var overrides DevTunnel when deploying outside local dev.

### Data layer
- All data is **in-memory** — JSON seed files under `Waha.McpServer/Data/` are loaded once by the `*Service` singletons at startup. There is no database.
- To replace the tour catalog, edit `tours.json`, `destinations.json`, `policies.json`.

### Agent persona
- Aria's system prompt is the `SystemPrompts.Aria` constant in `Waha.WebApi/Constants/SystemPrompts.cs`.
- Scheduling logic (departure reminders, post-trip feedback) lives in `Waha.WebApi/Scheduling/SchedulerService.cs` and delegates to `TravelBotHandler` / `FeedbackHandler`.

### Branch naming (contributing)
```
feat/<issue-number>-short-description
fix/<issue-number>-short-description
```
