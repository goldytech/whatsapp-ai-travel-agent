using System.Security.Cryptography;
using System.Text;

namespace AgentForge.WebApi.Services;

public sealed class WahaWebhookSignatureValidator(
    IOptions<WahaWebhookSecurityOptions> options,
    ILogger<WahaWebhookSignatureValidator> logger)
{
    private const string SignatureHeaderName = "X-Webhook-Hmac";
    private const string AlgorithmHeaderName = "X-Webhook-Hmac-Algorithm";
    private const string ExpectedAlgorithm = "sha512";
    private const int Sha512HashLengthBytes = 64;
    private readonly byte[] _secretKeyBytes = Encoding.UTF8.GetBytes(options.Value.Secret);

    public async Task<WahaWebhookValidationResult> ValidateAsync(HttpRequest request, CancellationToken ct)
    {
        if (!request.Headers.TryGetValue(SignatureHeaderName, out var signatureHeaderValues))
        {
            logger.LogWarning("Rejecting WAHA webhook: missing {HeaderName} header", SignatureHeaderName);
            return WahaWebhookValidationResult.Invalid("Missing webhook signature header.");
        }

        if (!request.Headers.TryGetValue(AlgorithmHeaderName, out var algorithmHeaderValues))
        {
            logger.LogWarning("Rejecting WAHA webhook: missing {HeaderName} header", AlgorithmHeaderName);
            return WahaWebhookValidationResult.Invalid("Missing webhook signature algorithm header.");
        }

        var algorithm = algorithmHeaderValues.ToString();
        if (!algorithm.Equals(ExpectedAlgorithm, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Rejecting WAHA webhook: unsupported HMAC algorithm '{Algorithm}'", algorithm);
            return WahaWebhookValidationResult.Invalid("Unsupported webhook signature algorithm.");
        }

        byte[] providedSignature;
        try
        {
            providedSignature = Convert.FromHexString(signatureHeaderValues.ToString());
        }
        catch (FormatException)
        {
            logger.LogWarning("Rejecting WAHA webhook: signature header is not valid hex");
            return WahaWebhookValidationResult.Invalid("Invalid webhook signature format.");
        }

        if (providedSignature.Length != Sha512HashLengthBytes)
        {
            logger.LogWarning(
                "Rejecting WAHA webhook: signature length {Length} bytes does not match SHA-512",
                providedSignature.Length);
            return WahaWebhookValidationResult.Invalid("Invalid webhook signature length.");
        }

        byte[] bodyBytes;
        await using (var bodyBuffer = new MemoryStream())
        {
            await request.Body.CopyToAsync(bodyBuffer, ct).ConfigureAwait(false);
            bodyBytes = bodyBuffer.ToArray();
        }

        var expectedSignature = HMACSHA512.HashData(_secretKeyBytes, bodyBytes);
        if (!CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
        {
            logger.LogWarning("Rejecting WAHA webhook: signature verification failed");
            return WahaWebhookValidationResult.Invalid("Invalid webhook signature.");
        }

        return WahaWebhookValidationResult.Valid(bodyBytes);
    }
}

public sealed record WahaWebhookValidationResult(bool IsValid, byte[]? BodyBytes, string? Error)
{
    public static WahaWebhookValidationResult Valid(byte[] bodyBytes) => new(true, bodyBytes, null);

    public static WahaWebhookValidationResult Invalid(string error) => new(false, null, error);
}
