using ModelContextProtocol.Server;
using Waha.McpServer.Services;

namespace Waha.McpServer.Tools;

[McpServerToolType]
public class PromotionTools(PromotionService promotionService, PolicyService policyService)
{
    [McpServerTool(Name = "get_active_promotions", ReadOnly = true)]
    [Description("Get current active promotions and special offers from Royal Journeys.")]
    public string GetActivePromotions()
    {
        var promos = promotionService.GetActivePromotions();
        if (promos.Count == 0)
            return "No active promotions at the moment. Standard pricing applies. Check back soon! 🎁";

        var lines = promos.Select(p =>
        {
            var discount = p.DiscountPercent > 0 ? $"{p.DiscountPercent}% off" : p.FreeAdd ?? "Special offer";
            return $"🎉 *{p.Title}*\n   {p.Description}\n   Discount: {discount} | Valid until: {p.ValidUntil}\n   {(p.PromoCode is not null ? $"Code: `{p.PromoCode}`" : "")}";
        });
        return $"✨ *Current Special Offers from Royal Journeys:*\n\n{string.Join("\n\n", lines)}";
    }

    [McpServerTool(Name = "calculate_group_discount", ReadOnly = true)]
    [Description("Calculate group discount for a given number of passengers on a tour.")]
    public string CalculateGroupDiscount(
        [Description("Tour ID")] string tourId,
        [Description("Number of passengers in the group")] int paxCount)
    {
        var agency = policyService.GetAgencyInfo();
        var tier = agency.GroupDiscountTiers
            .Where(t => paxCount >= t.MinPax && paxCount <= t.MaxPax)
            .FirstOrDefault();

        if (tier is null)
            return $"No group discount applies for {paxCount} pax (minimum 5 pax required for group rates).";

        return $"""
            👥 *Group Discount for {paxCount} pax:*
            Discount: *{tier.DiscountPercent}%*
            {(tier.DiscountPercent >= 15 ? "✅ Complimentary dedicated tour manager included!" : "")}

            Note: Group discounts cannot be combined with other promotions.
            Contact us to create a custom group quote for {tourId}!
            """;
    }
}
