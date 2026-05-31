namespace AgentForge.WebApi.Services;

internal static class PublicWebhookUrlResolver
{
    public static string? GetConfiguredBaseUrl(IConfiguration config)
        => config["WEBHOOK_BASE_URL"];

    public static string? GetBaseUrl(IConfiguration config)
        => GetConfiguredCandidates(config).FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

    public static bool TryGetBaseUri(IConfiguration config, out Uri baseUri)
        => TryGetBaseUri(GetConfiguredCandidates(config), out baseUri);

    public static bool TryGetBaseUri(HttpRequest request, IConfiguration config, out Uri baseUri)
        => TryGetBaseUri(
            [
                .. GetConfiguredCandidates(config),
                $"{request.Scheme}://{request.Host}"
            ],
            out baseUri);

    private static string?[] GetConfiguredCandidates(IConfiguration config) =>
    [
        config["WEBHOOK_HTTPS"],
        config["services:webhook:https:0"],
        config["services:waha-webhook:https:0"],
        config["WEBHOOK_BASE_URL"]
    ];

    private static bool TryGetBaseUri(IEnumerable<string?> candidates, out Uri baseUri)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)
                || !Uri.TryCreate(candidate.TrimEnd('/') + "/", UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https"))
            {
                continue;
            }

            baseUri = uri;
            return true;
        }

        baseUri = default!;
        return false;
    }
}
