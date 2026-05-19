using ModelContextProtocol.Server;
using Waha.McpServer.Services;

namespace Waha.McpServer.Tools;

[McpServerToolType]
public class PolicyTools(PolicyService policyService, TourCatalogService catalog)
{
    [McpServerTool(Name = "get_cancellation_policy", ReadOnly = true)]
    [Description("Get Royal Journeys cancellation and refund policy, optionally for a specific number of days before departure.")]
    public string GetCancellationPolicy(
        [Description("Days before departure (optional, to get the applicable refund tier)")] int? daysBeforeDeparture = null)
    {
        var policy = policyService.GetAgencyInfo().CancellationPolicy;

        if (daysBeforeDeparture.HasValue)
        {
            var tier = policy.Tiers
                .Where(t => daysBeforeDeparture.Value >= t.DaysBeforeDeparture)
                .OrderByDescending(t => t.DaysBeforeDeparture)
                .FirstOrDefault();

            if (tier is not null)
                return $"🔔 With {daysBeforeDeparture.Value} days before departure, you qualify for a *{tier.RefundPercent}% refund*.\n\n{policy.Note}";
        }

        var tiers = policy.Tiers.Select(t => $"  • {t.Label}: *{t.RefundPercent}% refund*");
        return $"""
            📋 *Royal Journeys Cancellation Policy:*

            {string.Join("\n", tiers)}

            {policy.Note}
            """;
    }

    [McpServerTool(Name = "get_inclusions_exclusions", ReadOnly = true)]
    [Description("Get what is and is not included in a specific tour package.")]
    public string GetInclusionsExclusions(
        [Description("Tour ID")] string tourId)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found.";

        return $"""
            📦 *What's Included — {tour.Name}*

            ✅ *INCLUDED:*
            {string.Join("\n", tour.Inclusions.Select(i => $"  • {i}"))}

            ❌ *NOT INCLUDED:*
            {string.Join("\n", tour.Exclusions.Select(e => $"  • {e}"))}
            """;
    }

    [McpServerTool(Name = "get_faq_answer", ReadOnly = true)]
    [Description("Answer frequently asked questions about Royal Journeys tours and policies.")]
    public string GetFaqAnswer(
        [Description("FAQ topic (e.g. 'cancellation', 'payment', 'health', 'documents', 'grouptravel')")] string topic,
        [Description("Specific question text (optional)")] string? question = null)
    {
        var answer = policyService.FindAnswer(topic, question);
        if (answer is null)
        {
            var allTopics = policyService.GetAllFaq().Select(t => t.Topic);
            return $"No FAQ found for topic '{topic}'. Available topics: {string.Join(", ", allTopics)}";
        }

        return $"❓ *{answer.Q}*\n\n{answer.A}";
    }
}
