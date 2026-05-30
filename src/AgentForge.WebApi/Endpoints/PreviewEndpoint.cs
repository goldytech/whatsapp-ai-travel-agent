using System.Text.Encodings.Web;
using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Endpoints;

/// <summary>
/// Serves a minimal HTML page with Open Graph meta tags for a given image URL.
/// <para>
/// WhatsApp's link-preview crawler fetches this page, reads the <c>og:image</c> tag,
/// and renders a native preview card in the chat — giving WAHA Core (free tier) a
/// visual image experience without WAHA Plus's <c>/api/sendImage</c>.
/// </para>
/// <para>
/// Security: <c>imageUrl</c> is validated as a local static image path under the active
/// vertical's configured asset prefix. Absolute URLs are accepted only when they point at
/// this app's configured public origin.
/// </para>
/// </summary>
public static class PreviewEndpoint
{
    public static IEndpointRouteBuilder MapPreviewEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/preview", (HttpRequest request, IConfiguration config, IVerticalDescriptor verticalDescriptor, string imageUrl, string? title, string? description) =>
        {
            if (!TryBuildLocalImageUri(request, config, verticalDescriptor, imageUrl, out var imageUri))
                return Results.BadRequest($"imageUrl must point to a local asset under '{verticalDescriptor.AssetPathPrefix}'.");

            var enc = HtmlEncoder.Default;
            var safeImage = enc.Encode(imageUri.ToString());
            var safeTitle = enc.Encode(title ?? verticalDescriptor.PreviewTitle);
            var safeDesc = enc.Encode(description ?? verticalDescriptor.PreviewDescription);

            var html = $$"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="utf-8">
                  <meta name="viewport" content="width=device-width,initial-scale=1">
                  <title>{{safeTitle}}</title>
                  <meta property="og:type"        content="website">
                  <meta property="og:title"       content="{{safeTitle}}">
                  <meta property="og:description" content="{{safeDesc}}">
                  <meta property="og:image"       content="{{safeImage}}">
                  <meta property="og:image:type"  content="image/jpeg">
                  <meta name="twitter:card"       content="summary_large_image">
                  <meta name="twitter:image"      content="{{safeImage}}">
                  <style>body{margin:0;background:#000;display:flex;justify-content:center;align-items:center;min-height:100vh}img{max-width:100%;max-height:100vh;object-fit:contain}</style>
                </head>
                <body>
                  <img src="{{safeImage}}" alt="{{safeTitle}}">
                </body>
                </html>
                """;

            return Results.Content(html, "text/html");
        })
        .AllowAnonymous()
        .WithName("ImagePreview");

        return app;
    }

    private static bool TryBuildLocalImageUri(
        HttpRequest request,
        IConfiguration config,
        IVerticalDescriptor verticalDescriptor,
        string imageUrl,
        out Uri imageUri)
    {
        imageUri = default!;

        if (string.IsNullOrWhiteSpace(imageUrl))
            return false;

        if (!TryGetPublicBaseUri(request, config, out var publicBaseUri))
            return false;

        var trimmedImageUrl = imageUrl.Trim();

        if (Uri.TryCreate(trimmedImageUrl, UriKind.Absolute, out var absoluteImageUri))
        {
            string imagePath;
            if (absoluteImageUri.Scheme is not ("http" or "https")
                || !IsSameOrigin(absoluteImageUri, publicBaseUri)
                || !TryGetSafeImagePath(absoluteImageUri.AbsolutePath, verticalDescriptor, out imagePath))
                return false;

            imageUri = new Uri(publicBaseUri, imagePath);
            return true;
        }

        if (!TryGetSafeImagePath(trimmedImageUrl, verticalDescriptor, out var relativeImagePath))
            return false;

        imageUri = new Uri(publicBaseUri, relativeImagePath);
        return true;
    }

    private static bool TryGetPublicBaseUri(HttpRequest request, IConfiguration config, out Uri publicBaseUri)
        => PublicWebhookUrlResolver.TryGetBaseUri(request, config, out publicBaseUri);

    private static bool TryGetSafeImagePath(
        string value,
        IVerticalDescriptor verticalDescriptor,
        out string imagePath)
    {
        imagePath = string.Empty;

        var path = Uri.UnescapeDataString(value.Trim());
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("//", StringComparison.Ordinal) || path.Contains('\\'))
            return false;

        path = "/" + path.TrimStart('/');

        if (!path.StartsWith(GetNormalizedAssetPathPrefix(verticalDescriptor), StringComparison.OrdinalIgnoreCase)
            || path.Contains("/../", StringComparison.Ordinal)
            || path.EndsWith("/..", StringComparison.Ordinal)
            || !Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out _))
            return false;

        imagePath = path;
        return true;
    }

    private static bool IsSameOrigin(Uri left, Uri right)
        => left.Scheme.Equals(right.Scheme, StringComparison.OrdinalIgnoreCase)
            && left.Host.Equals(right.Host, StringComparison.OrdinalIgnoreCase)
            && left.Port == right.Port;

    private static string GetNormalizedAssetPathPrefix(IVerticalDescriptor verticalDescriptor)
        => "/" + verticalDescriptor.AssetPathPrefix.Trim('/') + "/";
}
