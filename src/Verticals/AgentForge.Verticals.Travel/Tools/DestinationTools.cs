using ModelContextProtocol.Server;
using AgentForge.Verticals.Travel.Services;

namespace AgentForge.Verticals.Travel.Tools;

[McpServerToolType]
public class DestinationTools(DestinationService destinationService)
{
    [McpServerTool(Name = "get_destination_guide", ReadOnly = true)]
    [Description("Get a travel guide for a destination including best season, weather, attractions, and cuisine.")]
    public string GetDestinationGuide(
        [Description("Destination name (e.g. 'Goa', 'Kerala', 'Manali', 'Rajasthan', 'Andaman')")] string destination)
    {
        var guide = destinationService.GetByDestination(destination);
        return guide is null ? $"No detailed guide found for '{destination}'. Try one of: {string.Join(", ", destinationService.GetAll().Select(d => d.Destination))}" : $"""
             📍 *{guide.Destination}* — {guide.Region}

             🌤 *Best Time to Visit:* {guide.PeakSeason}
             🌡 *Temperature:* Winter: {guide.Weather.WinterTemp} | Summer: {guide.Weather.SummerTemp}
             💰 *Avg Budget/Day:* ₹{guide.AvgBudgetPerDayINR:N0}/person

             🏛 *Top Attractions:*
             {string.Join("\n", guide.TopAttractions.Select(a => $"  • {a}"))}

             🍽 *Must-try Cuisine:*
             {string.Join(", ", guide.Cuisine)}

             📅 *Avoid:* {guide.OffSeason}
             """;
    }

    [McpServerTool(Name = "get_visa_requirements", ReadOnly = true)]
    [Description("Get visa and travel permit requirements for a destination.")]
    public string GetVisaRequirements(
        [Description("Destination name")] string destination,
        [Description("Traveller's nationality (e.g. 'Indian', 'British', 'American')")] string nationality = "Indian")
    {
        var guide = destinationService.GetByDestination(destination);
        if (guide is null)
            return $"No visa information available for '{destination}'.";

        var policy = nationality.Contains("Indian", StringComparison.OrdinalIgnoreCase)
            ? guide.VisaPolicy.IndianNationals
            : guide.VisaPolicy.ForeignNationals;

        return $"🛂 *Visa Requirements — {destination}*\nFor {nationality} nationals:\n{policy}";
    }

    [McpServerTool(Name = "get_packing_recommendations", ReadOnly = true)]
    [Description("Get a packing list tailored to a specific destination and travel month.")]
    public string GetPackingRecommendations(
        [Description("Destination name")] string destination,
        [Description("Travel month (e.g. 'December', 'March')")] string travelMonth,
        [Description("Duration in days")] int durationDays = 7)
    {
        var guide = destinationService.GetByDestination(destination);
        if (guide is null)
            return $"No packing guide available for '{destination}'.";

        var packing = guide.PackingEssentials;
        return $"""
            🎒 *Packing List — {destination} ({travelMonth}, {durationDays} days)*

            👕 *Clothing:*
            {string.Join("\n", packing.Clothing.Select(c => $"  • {c}"))}

            🛠 *Gear & Essentials:*
            {string.Join("\n", packing.Gear.Select(g => $"  • {g}"))}

            📄 *Documents:*
            {string.Join("\n", packing.Documents.Select(d => $"  • {d}"))}

            🏥 *Health Tips:*
            {string.Join("\n", packing.Health.Select(h => $"  • {h}"))}
            """;
    }
}
