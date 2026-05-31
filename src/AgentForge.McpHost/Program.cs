using AgentForge.Verticals.Abstractions;
using AgentForge.Verticals.Hosting;
using ModelContextProtocol.Protocol;

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
builder.Services.AddSingleton<IVerticalMcpRegistrar>(sp => sp.GetRequiredService<IVerticalPlugin>().McpRegistrar);
builder.Services.AddSingleton<IVerticalDescriptor>(sp => sp.GetRequiredService<IVerticalPlugin>().CreateDescriptor(sp));
verticalPlugin.RegisterCommonServices(builder.Services);
verticalPlugin.McpRegistrar.RegisterServices(builder.Services);

// MCP Server with all tools and resources
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = verticalPlugin.ResolveMcpServerName(builder.Configuration), Version = "1.0.0" };
    })
    .WithToolsFromAssembly(verticalPlugin.McpRegistrar.McpAssembly)
    .WithResourcesFromAssembly(verticalPlugin.McpRegistrar.McpAssembly)
    .WithHttpTransport();

var app = builder.Build();
_ = app.Services.GetRequiredService<IVerticalDescriptor>();

app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();
