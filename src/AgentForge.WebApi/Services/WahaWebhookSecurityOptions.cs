using System.ComponentModel.DataAnnotations;

namespace AgentForge.WebApi.Services;

public sealed class WahaWebhookSecurityOptions
{
    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;
}
