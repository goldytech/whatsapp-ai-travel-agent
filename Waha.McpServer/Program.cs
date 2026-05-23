using ModelContextProtocol.Protocol;
using Waha.McpServer.Resources;
using Waha.McpServer.Services;
using Waha.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Data services
builder.Services.AddSingleton<TourCatalogService>();
builder.Services.AddSingleton<BookingInquiryService>();
builder.Services.AddSingleton<DestinationService>();
builder.Services.AddSingleton<HotelService>();
builder.Services.AddSingleton<PromotionService>();
builder.Services.AddSingleton<PolicyService>();

// MCP Server with all tools and resources
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation { Name = "RoyalJourneysMCP", Version = "1.0.0" };
    })
    .WithToolsFromAssembly(typeof(TourSearchTools).Assembly)
    .WithResourcesFromAssembly(typeof(TravelResources).Assembly)
    .WithHttpTransport();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapMcp("/mcp");

app.Run();
