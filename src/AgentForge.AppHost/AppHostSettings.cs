using Microsoft.Extensions.Configuration;

namespace AgentForge.AppHost;

internal sealed record AppHostSettings(
    bool IsPublishMode,
    string VerticalId,
    string VerticalPluginRoot,
    string VerticalPluginSourcePath,
    string VerticalPluginMountPath,
    string? CustomerConfigSourcePath,
    string CustomerConfigPath,
    string? ConfiguredWebhookBaseUrl,
    WahaTier WahaTier)
{
    public bool HasCustomerConfigPath => !string.IsNullOrWhiteSpace(CustomerConfigPath);

    public bool HasCustomerConfigSourcePath => !string.IsNullOrWhiteSpace(CustomerConfigSourcePath);

    public static AppHostSettings Load(IConfiguration configuration, bool isPublishMode)
    {
        var verticalId = configuration["VERTICAL_ID"] ?? "travel";
        var verticalPluginRoot = configuration["VERTICAL_PLUGIN_ROOT"] ?? "/app/plugins";
        var verticalPluginSourcePath = configuration["VERTICAL_PLUGIN_SOURCE_PATH"]
            ?? $"../../artifacts/plugins/{verticalId}";
        var verticalPluginMountPath = $"{verticalPluginRoot.TrimEnd('/')}/{verticalId}";
        var configuredCustomerConfigPath = configuration["CUSTOMER_CONFIG_PATH"];
        var customerConfigSourcePath = configuration["CUSTOMER_CONFIG_SOURCE_PATH"];
        var customerConfigPath = !string.IsNullOrWhiteSpace(configuredCustomerConfigPath)
            ? configuredCustomerConfigPath
            : isPublishMode && !string.IsNullOrWhiteSpace(customerConfigSourcePath) ? "/app/customer-config" : string.Empty;
        var configuredWebhookBaseUrl = configuration["WEBHOOK_BASE_URL"];
        var wahaTier = configuration["WahaTier"] is "Plus" ? WahaTier.Plus : WahaTier.Core;

        return new AppHostSettings(
            isPublishMode,
            verticalId,
            verticalPluginRoot,
            verticalPluginSourcePath,
            verticalPluginMountPath,
            customerConfigSourcePath,
            customerConfigPath,
            configuredWebhookBaseUrl,
            wahaTier);
    }
}
