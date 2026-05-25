using System.Text.Encodings.Web;

namespace Waha.WebApi.Endpoints;

/// <summary>
/// Serves a minimal HTML page with Open Graph meta tags for a given image URL.
/// <para>
/// WhatsApp's link-preview crawler fetches this page, reads the <c>og:image</c> tag,
/// and renders a native preview card in the chat — giving WAHA Core (free tier) a
/// visual image experience without WAHA Plus's <c>/api/sendImage</c>.
/// </para>
/// <para>
/// Security: <c>imageUrl</c> is validated to be same-origin (must start with the
/// request host). External URLs are rejected with <c>400 Bad Request</c>.
/// </para>
/// </summary>
public static class PreviewEndpoint
{
    public static IEndpointRouteBuilder MapPreviewEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/preview", (HttpRequest request, string imageUrl, string? title, string? description) =>
        {
            // Only allow same-origin image URLs to prevent open-redirect / SSRF
            var origin = $"{request.Scheme}://{request.Host}";
            if (!imageUrl.StartsWith(origin, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest("imageUrl must be same-origin.");

            var enc = HtmlEncoder.Default;
            var safeImage = enc.Encode(imageUrl);
            var safeTitle = enc.Encode(title ?? "Royal Journeys");
            var safeDesc = enc.Encode(description ?? "Discover amazing tour packages with Royal Journeys.");

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
}
