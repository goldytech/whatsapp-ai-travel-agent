using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace AgentForge.AppHost;

internal static class AppHostProjectResourceExtensions
{
    public static IResourceBuilder<ProjectResource> WithLocalVerticalInputs(
        this IResourceBuilder<ProjectResource> resource,
        AppHostLocalParameters? localParameters)
    {
        if (localParameters is null)
        {
            return resource;
        }

        return resource
            .WithEnvironment("VERTICAL_PLUGIN_PATH", localParameters.VerticalPluginPath)
            .WithEnvironment("CUSTOMER_CONFIG_PATH", localParameters.CustomerConfigPath);
    }

    public static IResourceBuilder<ProjectResource> WithPublishVerticalRuntime(
        this IResourceBuilder<ProjectResource> resource,
        AppHostSettings settings,
        string resourceName)
    {
        if (!settings.IsPublishMode)
        {
            return resource;
        }

        resource
            .WithEnvironment("VERTICAL_ID", settings.VerticalId)
            .WithEnvironment("VERTICAL_PLUGIN_ROOT", settings.VerticalPluginRoot)
            .WithEnvironment("VERTICAL_PLUGIN_PATH", settings.VerticalPluginMountPath);

        if (settings.HasCustomerConfigPath)
        {
            resource.WithEnvironment("CUSTOMER_CONFIG_PATH", settings.CustomerConfigPath);
        }

        return resource.PublishAsDockerComposeService((_, service) =>
        {
            service.Restart = "unless-stopped";
            service.AddVolume(CreateVerticalPluginVolume(settings, resourceName));

            if (TryCreateCustomerConfigVolume(settings, resourceName) is { } customerConfigVolume)
            {
                service.AddVolume(customerConfigVolume);
            }

            if (resourceName == "webhook")
            {
                service.Ports.Add("${WEBHOOK_HOST_PORT:-8080}:${WEBHOOK_PORT}");
                service.AddEnvironmentalVariable("WEBHOOK_BASE_URL", "${WEBHOOK_BASE_URL}");
                service.AddEnvironmentalVariable("WEBHOOK_PUBLIC_URL_MODE", "ConfiguredOnly");
            }
        });
    }

    private static Volume CreateVerticalPluginVolume(AppHostSettings settings, string resourceName) =>
        new()
        {
            Name = $"{settings.VerticalId}-plugin-{resourceName}",
            Type = "bind",
            Source = settings.VerticalPluginSourcePath,
            Target = settings.VerticalPluginMountPath,
            ReadOnly = true
        };

    private static Volume? TryCreateCustomerConfigVolume(AppHostSettings settings, string resourceName)
    {
        if (!settings.HasCustomerConfigSourcePath)
        {
            return null;
        }

        return new Volume
        {
            Name = $"{settings.VerticalId}-customer-config-{resourceName}",
            Type = "bind",
            Source = settings.CustomerConfigSourcePath,
            Target = settings.CustomerConfigPath,
            ReadOnly = true
        };
    }
}
