# Copilot Instructions — AgentForge Multi-Vertical Platform

## Build & Run

```bash
# Build entire solution (zero warnings expected)
dotnet build

# Start all services (Aspire orchestrates WAHA container, McpHost, WebApi, DevTunnel)
aspire start

# Generate Docker Compose deployment artifacts
aspire publish --apphost src/AgentForge.AppHost/AgentForge.AppHost.csproj -o artifacts/aspire-output

# Run a single project
dotnet run --project src/AgentForge.McpHost
dotnet run --project src/AgentForge.WebApi
```

There are no automated test projects in this repository. Verification is done by running the app and exercising the webhook manually (see `src/AgentForge.WebApi/AgentForge.WebApi.http`).

## Architecture Overview

This is a **.NET Aspire** solution with eight projects. The repository should now be treated as **AgentForge**, a reusable WhatsApp AI platform with a travel reference vertical currently shipped in-tree.

| Project | Role |
|---|---|
| `AgentForge.AppHost` | Aspire orchestrator — wires up all services, secrets, and the DevTunnel |
| `AgentForge.Hosting` | Custom Aspire integration (`AddWaha`) that encapsulates the WAHA Docker container |
| `AgentForge.McpHost` | Generic MCP host — loads tools/resources from the active vertical plugin and exposes them over `StreamableHttp` at `/mcp` |
| `AgentForge.WebApi` | Generic AI gateway — receives WhatsApp webhooks, runs the active vertical agent, sends replies |
| `AgentForge.ServiceDefaults` | Shared defaults — OpenTelemetry, health checks, HTTP resilience, service discovery |
| `AgentForge.Verticals.Abstractions` | Shared contracts for vertical metadata, scheduled actions, and host/plugin messaging |
| `AgentForge.Verticals.Hosting` | Shared loader layer that resolves the active in-process vertical for both hosts |
| `AgentForge.Verticals.Travel` | Current in-tree travel vertical implementation: agent metadata, prompt, tools, resources, data, and scheduled action behavior |

### Message flow

```
WhatsApp → WAHA container → DevTunnel → /webhook (WebApi)
    → WhatsAppMessageQueue (Channel<T>)
    → AgentChatService → VerticalAgentFactory (active vertical agent / ChatClientAgent)
        → AgentForge.McpHost (tools/resources from active vertical plugin over StreamableHttp)
        → Azure AI Foundry (GPT-5.4 mini)
    → WahaApiClient → WAHA → WhatsApp
```

**Key design decisions:**
- The `WhatsAppMessageQueue` is a bounded `Channel<T>` (capacity 200, `DropOldest`). The webhook returns `200 OK` immediately and processing happens asynchronously in a `BackgroundService`.
- Conversation history is **client-managed** (`AgentSessionStore`, keyed by phone number). The Azure AI Foundry chat completions API does not support server-managed history — do not use the `conversationId` overload of `CreateSessionAsync`.
- **Retries are intentionally disabled** on all HTTP clients (via `ServiceDefaults`). Retrying `WahaApiClient.SendTextAsync` would deliver the same WhatsApp message multiple times.
- `VerticalAgentFactory` uses double-checked locking (`SemaphoreSlim`) to lazily initialise the `ChatClientAgent` for the active vertical exactly once — MCP tool discovery is async and cannot happen in a constructor.
- Runtime vertical selection is controlled by `AgentForge.Verticals.Hosting`: `VERTICAL_PLUGIN_PATH`, or `VERTICAL_PLUGIN_ROOT` + `VERTICAL_ID`, or fallback to the in-tree travel plugin.

## Key Conventions

### C# style
- **Primary constructors** on all services (no `private readonly` field boilerplate for injected deps).
- **`ConfigureAwait(false)`** on every `await` call inside services and library code.
- **C# 14 features** where appropriate: `field`-backed properties, extension members, `System.Threading.Lock`, collection expressions (`[.. items]`), etc.
- No `#pragma warning disable` — fix the root cause.

### DI lifetime rules
- Stateful classes → `Singleton`
- Per-request / per-message work (e.g., `AgentChatService`) → `Scoped` (resolved per `IServiceScope` inside the queue's `BackgroundService`)
- Never `Transient` for stateful classes

### HTTP clients
- Always registered via `IHttpClientFactory` — never `new HttpClient()`.
- Service-discovery names match Aspire resource names: `"http://waha"`, `"http://mcpserver"`.

### Vertical plugin authoring
- New industries should be implemented as separate vertical libraries under `src/Verticals/AgentForge.Verticals.<Vertical>/`.
- The plugin contract lives in `src/AgentForge.Verticals.Abstractions/VerticalContracts.cs`.
- A vertical should implement `IVerticalPlugin`, expose an `IVerticalDescriptor`, expose an `IVerticalMcpRegistrar`, and register any WebApi-specific services it needs.
- The current travel plugin entry point is `src/Verticals/AgentForge.Verticals.Travel/TravelVerticalPlugin.cs`.

### MCP tool authoring (current travel vertical example: `src/Verticals/AgentForge.Verticals.Travel/Tools/`)
- Decorate the class with `[McpServerToolType]` and each method with `[McpServerTool]`.
- All parameters and descriptions use `[Description("...")]` attributes.
- Tools are auto-registered via `WithToolsFromAssembly` — no registration needed in `AgentForge.WebApi`.
- Return plain strings (WhatsApp-friendly, use `*bold*` and emoji). Use raw string literals (`"""..."""`) for multi-line responses.
- Read-only tools get `ReadOnly = true` on `[McpServerTool]`.

### Background work
- Use `Channel<T>` or `IHostedService` — never `_ = Task.Run(...)`.
- Register background services as singletons and add them with `AddHostedService(sp => sp.GetRequiredService<T>())` so the same instance is both injectable and hosted.

### Secrets & configuration
- All secrets live in **`AgentForge.AppHost` user secrets** (`dotnet user-secrets` in that project).
- The `ai-foundry` connection string format: `Endpoint=https://...;Key=...` (optional `DeploymentId=...`).
- `WEBHOOK_BASE_URL` env var overrides DevTunnel when deploying outside local dev.
- `wahaWebhookSecret` is a required Aspire secret parameter; `AgentForge.WebApi` uses it both to configure WAHA webhook HMAC signing and to verify incoming `X-Webhook-Hmac` requests against the raw request body.
- `VERTICAL_PLUGIN_PATH` can point `AgentForge.WebApi` and `AgentForge.McpHost` at an external published plugin folder or DLL; when unset, `AgentForge.Verticals.Hosting` falls back to the in-tree travel plugin.
- For Compose publishing, `AgentForge.AppHost` also supports `VERTICAL_ID`, `VERTICAL_PLUGIN_ROOT`, and `VERTICAL_PLUGIN_SOURCE_PATH`; publish mode bind-mounts the selected plugin into both hosts with `PublishAsDockerComposeService`.
- Do not exclude the custom `WahaResource` from the manifest — `aspire publish` needs it to generate the `waha` Compose service and resolve `webhook`'s service reference correctly.

### Data layer
- All data is **in-memory** today — the current travel plugin loads JSON seed files under `src/Verticals/AgentForge.Verticals.Travel/Data/` once at startup. There is no database yet.
- Future verticals may ship their own `Data/` folders and deployment validation rules inside their plugin assemblies.

### Agent persona
- The active agent's system prompt, preview defaults, and branding come from the selected vertical descriptor.
- The current travel example lives in `src/Verticals/AgentForge.Verticals.Travel/TravelVerticalDescriptor.cs`.
- Scheduling logic lives in `src/AgentForge.WebApi/Scheduling/SchedulerService.cs` and dispatches through `IScheduledActionHandler`, currently implemented by `TravelScheduledActionHandler` in `src/Verticals/AgentForge.Verticals.Travel/`.

### Documentation positioning
- Treat the repository as **platform-first** in documentation and future codegen/review work.
- Travel is the current reference vertical and first commercial wedge, not the permanent identity of the codebase.

### Branch naming (contributing)
```
feat/<issue-number>-short-description
fix/<issue-number>-short-description
```
