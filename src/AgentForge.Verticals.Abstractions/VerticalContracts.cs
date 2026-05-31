using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentForge.Verticals.Abstractions;

public interface IVerticalDescriptor
{
    string VerticalId { get; }
    string DisplayName { get; }
    string AgentName { get; }
    string AgentDescription { get; }
    string SystemPrompt { get; }
    string McpServerName { get; }
    string AssetPathPrefix { get; }
    string PreviewTitle { get; }
    string PreviewDescription { get; }
}

public interface IMessageSender
{
    Task SendTextAsync(string chatId, string text, CancellationToken ct = default);

    Task SendImageAsync(string chatId, string imageUrl, string? caption = null, CancellationToken ct = default);
}

public interface IScheduledActionHandler
{
    Task HandleAsync(ScheduledAction action, CancellationToken ct = default);
}

public interface IVerticalMcpRegistrar
{
    Assembly McpAssembly { get; }

    void RegisterServices(IServiceCollection services);
}

public interface IVerticalPlugin
{
    IVerticalMcpRegistrar McpRegistrar { get; }

    void ConfigureConfiguration(IConfigurationManager configuration);

    void RegisterCommonServices(IServiceCollection services);

    void RegisterWebApiServices(IServiceCollection services);

    IVerticalDescriptor CreateDescriptor(IServiceProvider serviceProvider);

    string ResolveMcpServerName(IConfiguration configuration);
}

public interface IVerticalPluginLoader
{
    IVerticalPlugin Load();
}

public interface IVerticalDeploymentValidator
{
    void ValidateDeployment();
}

public sealed record ScheduledAction(
    string ChatId,
    string ActionType,
    string ItemName,
    DateTimeOffset ScheduledAt);
