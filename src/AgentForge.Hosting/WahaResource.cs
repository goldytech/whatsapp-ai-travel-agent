using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents a WAHA (WhatsApp HTTP API) container resource.
/// Implements <see cref="IResourceWithConnectionString"/> so consumers can inject the
/// WAHA base URL via <c>WithReference</c>.
/// </summary>
public sealed class WahaResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    internal const string HttpEndpointName = "http";

    private EndpointReference? _primaryEndpoint;

    /// <summary>Gets the primary HTTP endpoint for this WAHA instance.</summary>
    public EndpointReference PrimaryEndpoint =>
        _primaryEndpoint ??= new EndpointReference(this, HttpEndpointName);

    /// <summary>
    /// Connection string expression resolves to <c>http://host:port</c> at runtime.
    /// Consumers receive this as <c>ConnectionStrings__waha</c>.
    /// </summary>
    public ReferenceExpression ConnectionStringExpression =>
        ReferenceExpression.Create(
            $"{PrimaryEndpoint.Property(EndpointProperty.Scheme)}://{PrimaryEndpoint.Property(EndpointProperty.Host)}:{PrimaryEndpoint.Property(EndpointProperty.Port)}");
}
