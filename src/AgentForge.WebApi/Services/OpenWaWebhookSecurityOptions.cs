using System.ComponentModel.DataAnnotations;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaWebhookSecurityOptions
{
    [Required]
    public string Secret { get; set; } = string.Empty;
}
