using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Publishing;
using Microsoft.Extensions.Configuration;

namespace AgentForge.AppHost;

internal sealed class AppHostLocalParameters(
    IResourceBuilder<ParameterResource> verticalPluginPath,
    IResourceBuilder<ParameterResource> customerConfigPath)
{
    public IResourceBuilder<ParameterResource> VerticalPluginPath { get; } = verticalPluginPath;

    public IResourceBuilder<ParameterResource> CustomerConfigPath { get; } = customerConfigPath;

    public static AppHostLocalParameters? Create(IDistributedApplicationBuilder builder, bool isPublishMode)
    {
        if (isPublishMode)
        {
            return null;
        }

        var verticalPluginPath = AddOptionalPathOverride(
            builder,
            parameterName: "vertical-plugin-path",
            legacyConfigurationKey: "VERTICAL_PLUGIN_PATH",
            label: "Vertical plugin path",
            description: "Optional local override for an external vertical plugin folder or DLL. Leave blank to use the built-in in-tree vertical.",
            placeholder: "/Users/you/.../artifacts/plugins/travel");
        var customerConfigPath = AddOptionalPathOverride(
            builder,
            parameterName: "customer-config-path",
            legacyConfigurationKey: "CUSTOMER_CONFIG_PATH",
            label: "Customer config path",
            description: "Optional local override for a customer config folder containing customer-profile.json and an optional prompt.md. Leave blank to use the bundled defaults.",
            placeholder: "/Users/you/.../customer-config");

        return new AppHostLocalParameters(verticalPluginPath, customerConfigPath);
    }

    private static IResourceBuilder<ParameterResource> AddOptionalPathOverride(
        IDistributedApplicationBuilder builder,
        string parameterName,
        string legacyConfigurationKey,
        string label,
        string description,
        string placeholder)
    {
        var parameter = builder.AddParameter(
                parameterName,
                new LegacyAwareOptionalPathDefault(builder.Configuration, legacyConfigurationKey))
            .WithDescription(description)
            .WithCustomInput(resource => new InteractionInput
            {
                InputType = InputType.Text,
                Name = resource.Name,
                Label = label,
                Value = ResolveCurrentValue(builder.Configuration, parameterName, legacyConfigurationKey),
                Placeholder = placeholder,
                Description = resource.Description,
                EnableDescriptionMarkdown = resource.EnableDescriptionMarkdown
            });

        return parameter;
    }

    private static string ResolveCurrentValue(
        IConfiguration configuration,
        string parameterName,
        string legacyConfigurationKey) =>
        configuration[$"Parameters:{parameterName}"]
        ?? configuration[legacyConfigurationKey]
        ?? string.Empty;

    private sealed class LegacyAwareOptionalPathDefault(
        IConfiguration configuration,
        string legacyConfigurationKey) : ParameterDefault
    {
        public override string GetDefaultValue() => configuration[legacyConfigurationKey] ?? string.Empty;

        public override void WriteToManifest(ManifestPublishingContext context) =>
            context.Writer.WriteString("value", GetDefaultValue());
    }
}
