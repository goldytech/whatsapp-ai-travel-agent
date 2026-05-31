namespace AgentForge.Verticals.Travel.Models;

public record Promotion(
    string Id, string Title, string Description,
    int DiscountPercent, string Type,
    string ValidFrom, string ValidUntil,
    string[] ApplicableTours, string Conditions,
    string? PromoCode = null, string? FreeAdd = null, string? AppliesTo = null
);
