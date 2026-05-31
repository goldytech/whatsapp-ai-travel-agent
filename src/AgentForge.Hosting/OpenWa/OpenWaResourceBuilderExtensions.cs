using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding and configuring OpenWA resources in an Aspire AppHost.
/// </summary>
public static class OpenWaResourceBuilderExtensions
{
    public const string DefaultVersion = "0.1.6";

    private const string ApiImage = "rmyndharis/openwa";
    private const string DataVolumeMountPath = "/app/data";
    private const string DashboardContextPath = "../AgentForge.Hosting/OpenWa/Dashboard";

    /// <summary>
    /// Adds the OpenWA API container resource to the distributed application.
    /// </summary>
    public static IResourceBuilder<OpenWaResource> AddOpenWa(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> apiKey,
        IResourceBuilder<ParameterResource> encryptionKey,
        string version = DefaultVersion,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(encryptionKey);

        var resource = new OpenWaResource(name);
        var resourceBuilder = builder
            .AddResource(resource)
            .WithImage(ApiImage, version)
            .WithImageRegistry("ghcr.io")
            .WithHttpEndpoint(port: port, targetPort: 2785, name: OpenWaResource.HttpEndpointName)
            .WithEnvironment("NODE_ENV", "production")
            .WithEnvironment("PORT", "2785")
            .WithEnvironment("API_PREFIX", "/api")
            .WithEnvironment("API_MASTER_KEY", apiKey)
            .WithEnvironment("API_KEY_MASTER", apiKey)
            .WithEnvironment("ENCRYPTION_KEY", encryptionKey)
            .WithEnvironment("PUPPETEER_HEADLESS", "true")
            .WithEnvironment("PUPPETEER_ARGS", "--no-sandbox,--disable-setuid-sandbox,--disable-dev-shm-usage,--disable-gpu")
            .WithHttpHealthCheck("/health");

        var endpoint = resource.PrimaryEndpoint;
        resourceBuilder.WithUrl($"{endpoint}/api/docs", "OpenWA Swagger");

        return resourceBuilder;
    }

    /// <summary>
    /// Adds the OpenWA dashboard container built from the upstream dashboard source.
    /// </summary>
    public static IResourceBuilder<ContainerResource> AddOpenWaDashboard(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<OpenWaResource> openWa,
        string version = DefaultVersion,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(openWa);

        var resourceBuilder = builder
            .AddDockerfile(name, DashboardContextPath)
            .WithBuildArg("OPENWA_VERSION", version)
            .WithHttpEndpoint(port: port, targetPort: 80, name: "http")
            .WaitFor(openWa);

        var endpoint = resourceBuilder.Resource.GetEndpoint("http");
        resourceBuilder.WithUrl($"{endpoint}", "OpenWA Dashboard");

        return resourceBuilder;
    }

    /// <summary>
    /// Persists OpenWA runtime data, including session auth state, across container restarts.
    /// </summary>
    public static IResourceBuilder<OpenWaResource> WithDataVolume(
        this IResourceBuilder<OpenWaResource> builder,
        string volumeName = "openwa-data")
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(volumeName, DataVolumeMountPath);
    }

    /// <summary>
    /// Keeps the OpenWA container stable across AppHost restarts so the QR-authenticated session survives.
    /// </summary>
    public static IResourceBuilder<OpenWaResource> WithPersistentLifetime(
        this IResourceBuilder<OpenWaResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithLifetime(ContainerLifetime.Persistent);
    }

    /// <summary>
    /// Configures OpenWA to use PostgreSQL for its provider-side persistence.
    /// </summary>
    public static IResourceBuilder<OpenWaResource> WithPostgres(
        this IResourceBuilder<OpenWaResource> builder,
        IResourceBuilder<PostgresDatabaseResource> database)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(database);

        return builder
            .WithEnvironment("DATABASE_TYPE", "postgres")
            .WithEnvironment("DATABASE_URL", database.Resource.ConnectionStringExpression);
    }

    /// <summary>
    /// Configures OpenWA to use Redis for cache and queue-related provider features.
    /// </summary>
    public static IResourceBuilder<OpenWaResource> WithRedis(
        this IResourceBuilder<OpenWaResource> builder,
        IResourceBuilder<RedisResource> redis)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(redis);

        return builder
            .WithEnvironment("CACHE_TYPE", "redis")
            .WithEnvironment("REDIS_ENABLED", "true")
            .WithEnvironment("REDIS_URL", redis.Resource.ConnectionStringExpression);
    }
}
