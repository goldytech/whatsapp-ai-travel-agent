using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaWebhookSignatureValidator(
    IOptions<OpenWaWebhookSecurityOptions> options,
    ILogger<OpenWaWebhookSignatureValidator> logger)
{
    private const string SignatureHeaderName = "X-OpenWA-Signature";
    private const string SignaturePrefix = "sha256=";

    public async Task<OpenWaWebhookValidationResult> ValidateAsync(
        HttpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Headers.TryGetValue(SignatureHeaderName, out var headerValues))
        {
            logger.LogWarning("Webhook rejected because {HeaderName} was missing", SignatureHeaderName);
            return OpenWaWebhookValidationResult.Invalid;
        }

        var providedSignature = headerValues.ToString().Trim();
        if (!providedSignature.StartsWith(SignaturePrefix, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Webhook rejected because {HeaderName} did not include the expected sha256 prefix", SignatureHeaderName);
            return OpenWaWebhookValidationResult.Invalid;
        }

        request.EnableBuffering();
        await using var bodyBuffer = new MemoryStream();
        await request.Body.CopyToAsync(bodyBuffer, cancellationToken).ConfigureAwait(false);
        request.Body.Position = 0;

        var bodyBytes = bodyBuffer.ToArray();
        var secretBytes = Encoding.UTF8.GetBytes(options.Value.Secret);
        var computedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        var expectedSignature = SignaturePrefix + Convert.ToHexString(computedHash).ToLowerInvariant();
        var normalizedProvidedSignature = SignaturePrefix + providedSignature[SignaturePrefix.Length..].ToLowerInvariant();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(normalizedProvidedSignature)))
        {
            logger.LogWarning("Webhook rejected because {HeaderName} did not match the expected HMAC", SignatureHeaderName);
            return OpenWaWebhookValidationResult.Invalid;
        }

        return new OpenWaWebhookValidationResult(true, bodyBytes);
    }
}

public readonly record struct OpenWaWebhookValidationResult(bool IsValid, byte[]? BodyBytes)
{
    public static OpenWaWebhookValidationResult Invalid { get; } = new(false, null);
}
