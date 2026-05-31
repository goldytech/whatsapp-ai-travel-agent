using AgentForge.Verticals.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentForge.Verticals.Travel;

public sealed class TravelVerticalPlugin : IVerticalPlugin
    , IVerticalDeploymentValidator
{
    public IVerticalMcpRegistrar McpRegistrar { get; } = new TravelMcpRegistrar();

    public void ConfigureConfiguration(IConfigurationManager configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        TravelConfigurationFiles.AddConfigurationSources(configuration);
    }

    public void RegisterCommonServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<TravelCustomerProfileOptions>, TravelCustomerProfileOptionsValidator>();
        services.AddOptions<TravelCustomerProfileOptions>()
            .BindConfiguration(TravelCustomerProfileOptions.SectionName)
            .ValidateOnStart();
    }

    public void RegisterWebApiServices(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IScheduledActionHandler, TravelScheduledActionHandler>();
    }

    public IVerticalDescriptor CreateDescriptor(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var options = serviceProvider.GetRequiredService<IOptions<TravelCustomerProfileOptions>>().Value;
        var promptPath = TravelConfigurationFiles.ResolvePromptPath(configuration, options.PromptFileName);
        var prompt = File.ReadAllText(promptPath);

        return new ResolvedTravelVerticalDescriptor(options, prompt);
    }

    public string ResolveMcpServerName(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return TravelCustomerProfileBinder.ResolveRequiredMcpServerName(configuration);
    }

    public void ValidateDeployment()
    {
        TravelDataFiles.EnsureDataDirectoryExists();
        TravelConfigurationFiles.EnsureDefaultFilesExist();
    }
}
