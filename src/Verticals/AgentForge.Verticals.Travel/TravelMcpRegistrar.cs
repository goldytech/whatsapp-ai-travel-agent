using System.Reflection;
using AgentForge.Verticals.Abstractions;
using AgentForge.Verticals.Travel.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Verticals.Travel;

public sealed class TravelMcpRegistrar : IVerticalMcpRegistrar
{
    public Assembly McpAssembly => typeof(TravelMcpRegistrar).Assembly;

    public void RegisterServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TourCatalogService>();
        services.AddSingleton<BookingInquiryService>();
        services.AddSingleton<DestinationService>();
        services.AddSingleton<HotelService>();
        services.AddSingleton<PromotionService>();
        services.AddSingleton<PolicyService>();
    }
}
