using ModelContextProtocol.Server;
using Waha.McpServer.Services;

namespace Waha.McpServer.Tools;

[McpServerToolType]
public class TourSearchTools(TourCatalogService catalog)
{
    [McpServerTool(Name = "search_tours", ReadOnly = true)]
    [Description("Search available tours by destination, query, budget or travel month. Returns a summary list.")]
    public string SearchTours(
        [Description("Destination name (e.g. 'Goa', 'Kerala', 'Manali')")] string? destination = null,
        [Description("Free-text query (e.g. 'beach family', 'snow adventure', 'honeymoon')")] string? query = null,
        [Description("Maximum budget per person in INR")] decimal? maxBudget = null,
        [Description("Travel month in YYYY-MM format (e.g. '2025-12')")] string? travelMonth = null,
        [Description("Maximum duration in days")] int? maxDurationDays = null)
    {
        var results = catalog.Search(destination, query, maxBudget, maxDurationDays, travelMonth);
        if (results.Count == 0)
            return "No tours found matching your criteria. Try relaxing one of the filters.";

        var lines = results.Select(t =>
            $"• *{t.Name}* (ID: {t.Id}) | {t.Destination} | {t.Duration} | ₹{t.Price:N0}/person | Tags: {string.Join(", ", t.Tags)}");
        return $"Found {results.Count} tour(s):\n\n{string.Join("\n", lines)}";
    }

    [McpServerTool(Name = "get_tour_details", ReadOnly = true)]
    [Description("Get full details for a specific tour including highlights, inclusions, exclusions, and reviews.")]
    public string GetTourDetails(
        [Description("Tour ID (e.g. 'GOA-DEC', 'MANALI-WINTER')")] string tourId)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found. Use search_tours to find valid tour IDs.";

        var reviews = tour.Reviews.Length > 0
            ? string.Join("\n", tour.Reviews.Take(2).Select(r => $"  ⭐ {r.Rating}/5 – \"{r.Comment}\" — {r.Author}"))
            : "No reviews yet.";

        var availability = tour.Availability.Any()
            ? string.Join(", ", tour.Availability.Select(kv => $"{kv.Key}: {kv.Value} slots"))
            : "Contact us for availability.";

        return $"""
            *{tour.Name}* 🗺️
            📍 {tour.Destination} | ⏱ {tour.Duration} | 💰 ₹{tour.Price:N0}/person
            Single supplement: ₹{tour.SingleSupplement:N0}

            ✅ *Highlights:*
            {string.Join("\n", tour.Highlights.Select(h => $"  • {h}"))}

            ✅ *Inclusions:*
            {string.Join("\n", tour.Inclusions.Select(i => $"  • {i}"))}

            ❌ *Exclusions:*
            {string.Join("\n", tour.Exclusions.Select(e => $"  • {e}"))}

            📅 *Availability:*
            {availability}

            ⭐ *Recent Reviews:*
            {reviews}
            """;
    }

    [McpServerTool(Name = "check_availability", ReadOnly = true)]
    [Description("Check remaining slots for a tour in a specific travel month.")]
    public string CheckAvailability(
        [Description("Tour ID")] string tourId,
        [Description("Travel month in YYYY-MM format")] string travelMonth)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found.";

        if (tour.Availability.TryGetValue(travelMonth, out var slots))
        {
            if (slots == 0)
                return $"❌ {tour.Name} is fully booked for {travelMonth}. Next available months: {string.Join(", ", tour.Availability.Where(kv => kv.Value > 0).Select(kv => $"{kv.Key} ({kv.Value} slots)"))}";
            return $"✅ {tour.Name} has *{slots} slot(s)* available for {travelMonth}. Book soon before they fill up!";
        }

        var nextAvailable = tour.Availability.Where(kv => kv.Value > 0)
            .Select(kv => $"{kv.Key} ({kv.Value} slots)")
            .Take(3);
        return $"No scheduled departures for {tour.Name} in {travelMonth}. Available months: {string.Join(", ", nextAvailable)}";
    }

    [McpServerTool(Name = "get_pricing_breakdown", ReadOnly = true)]
    [Description("Get a detailed cost breakdown for a tour including room sharing options and taxes.")]
    public string GetPricingBreakdown(
        [Description("Tour ID")] string tourId,
        [Description("Number of passengers")] int paxCount,
        [Description("Room type: 'double' (sharing), 'single' (own room), 'triple' (3 to a room)")] string roomType = "double")
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found.";

        var basePerPerson = tour.Price;
        var supplement = roomType.ToLower() == "single" ? tour.SingleSupplement : 0;
        var totalPerPerson = basePerPerson + supplement;
        var gst = totalPerPerson * 0.05m; // 5% GST on tour packages
        var grandTotalPerPerson = totalPerPerson + gst;
        var grandTotal = grandTotalPerPerson * paxCount;

        return $"""
            💰 *Pricing Breakdown — {tour.Name}*
            Room type: {roomType.ToUpper()} | Pax: {paxCount}

            Per person:
              Base price:         ₹{basePerPerson:N0}
              Single supplement:  ₹{supplement:N0}
              GST (5%):           ₹{gst:N2}
              *Total per person:  ₹{grandTotalPerPerson:N0}*

            *Grand total ({paxCount} pax): ₹{grandTotal:N0}*

            💡 25% deposit (₹{grandTotal * 0.25m:N0}) confirms the booking.
            Balance due 30 days before departure.
            """;
    }
}
