using Aspire.Hosting.ApplicationModel;

namespace Aspire.Hosting;

/// <summary>
/// Represents an OpenWA API container resource in the Aspire application model.
/// </summary>
public sealed class OpenWaResource(string name) : ContainerResource(name), IResourceWithConnectionString
{
    public const string HttpEndpointName = "http";

    public EndpointReference PrimaryEndpoint => this.GetEndpoint(HttpEndpointName);

    public ReferenceExpression ConnectionStringExpression => ReferenceExpression.Create($"{PrimaryEndpoint}");
}
