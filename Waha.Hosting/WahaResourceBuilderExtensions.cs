using System.Runtime.InteropServices;
using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Extension methods for adding and configuring a <see cref="WahaResource"/> in an Aspire AppHost.
/// </summary>
public static class WahaResourceBuilderExtensions
{
    private const string Image = "devlikeapro/waha";
    private const string SessionsVolumeMountPath = "/app/.sessions";

    /// <summary>
    /// Adds a WAHA (WhatsApp HTTP API) container resource to the distributed application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The resource name (used for service discovery).</param>
    /// <param name="apiKey">Secret parameter for <c>WAHA_API_KEY</c>.</param>
    /// <param name="dashboardPassword">Secret parameter for the dashboard password.</param>
    /// <param name="swaggerPassword">Secret parameter for the Swagger UI password.</param>
    /// <param name="engine">The WAHA engine to use. Defaults to <see cref="WahaEngine.NOWEB"/>.</param>
    /// <param name="port">Optional host port mapping (defaults to any free port).</param>
    public static IResourceBuilder<WahaResource> AddWaha(
        this IDistributedApplicationBuilder builder,
        [ResourceName] string name,
        IResourceBuilder<ParameterResource> apiKey,
        IResourceBuilder<ParameterResource> dashboardPassword,
        IResourceBuilder<ParameterResource> swaggerPassword,
        WahaEngine engine = WahaEngine.NOWEB,
        int? port = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(apiKey);
        ArgumentNullException.ThrowIfNull(dashboardPassword);
        ArgumentNullException.ThrowIfNull(swaggerPassword);

        var resource = new WahaResource(name);
        var tag = ResolveImageTag(engine);

        var resourceBuilder = builder
            .AddResource(resource)
            .WithAnnotation(new ContainerImageAnnotation { Image = Image, Tag = tag })
            .WithHttpEndpoint(port: port, targetPort: 3000, name: WahaResource.HttpEndpointName)
            .WithEnvironment("WAHA_API_KEY", apiKey)
            .WithEnvironment("WAHA_DASHBOARD_USERNAME", "admin")
            .WithEnvironment("WAHA_DASHBOARD_PASSWORD", dashboardPassword)
            .WithEnvironment("WHATSAPP_SWAGGER_USERNAME", "admin")
            .WithEnvironment("WHATSAPP_SWAGGER_PASSWORD", swaggerPassword)
            .WithHttpHealthCheck("/ping")
            .ExcludeFromManifest();

        // WithUrl uses the ReferenceExpression interpolated-string overload so URL resolution
        // is deferred to runtime (after port allocation). Do NOT use endpoint.Url here.
        var endpoint = resource.PrimaryEndpoint;
        resourceBuilder
            .WithUrl($"{endpoint}/dashboard", "WAHA Dashboard")
            .WithUrl($"{endpoint}", "WAHA Swagger");

        return resourceBuilder;
    }

    /// <summary>
    /// Mounts a named Docker volume at the WAHA sessions directory so that the
    /// WhatsApp session survives container restarts.
    /// </summary>
    /// <param name="builder">The WAHA resource builder.</param>
    /// <param name="volumeName">The Docker volume name. Defaults to <c>waha-sessions</c>.</param>
    public static IResourceBuilder<WahaResource> WithDataVolume(
        this IResourceBuilder<WahaResource> builder,
        string volumeName = "waha-sessions")
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithVolume(volumeName, SessionsVolumeMountPath);
    }

    /// <summary>
    /// Sets the container lifetime to <see cref="ContainerLifetime.Persistent"/> so Aspire
    /// does not recreate the container on every AppHost restart (preserves QR auth).
    /// </summary>
    public static IResourceBuilder<WahaResource> WithPersistentLifetime(
        this IResourceBuilder<WahaResource> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithLifetime(ContainerLifetime.Persistent);
    }

    /// <summary>
    /// Returns the Docker image tag for the given engine, automatically selecting the
    /// <c>-arm</c> variant when running on Apple Silicon or other ARM64 hosts.
    /// </summary>
    private static string ResolveImageTag(WahaEngine engine)
    {
        bool isArm = RuntimeInformation.OSArchitecture is Architecture.Arm64 or Architecture.Arm;

        return engine switch
        {
            WahaEngine.WEBJS => isArm ? "arm" : "latest",
            WahaEngine.WPP   => isArm ? "wpp-arm" : "wpp",
            WahaEngine.NOWEB => isArm ? "noweb-arm" : "noweb",
            // GOWS has no ARM image — always use the standard tag.
            WahaEngine.GOWS  => "gows",
            _                => "latest",
        };
    }
}
