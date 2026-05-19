using ModelContextProtocol.Server;
using Waha.McpServer.Services;

namespace Waha.McpServer.Resources;

[McpServerResourceType]
public class TravelResources(TourCatalogService catalog, DestinationService destinationService, PolicyService policyService)
{
    [McpServerResource(UriTemplate = "tour://catalog", Name = "Tour Catalog", MimeType = "text/plain")]
    [Description("Complete list of all available Royal Journeys tours.")]
    public string GetTourCatalog()
    {
        var tours = catalog.GetAll();
        var lines = tours.Select(t =>
            $"• {t.Name} (ID: {t.Id}) | {t.Destination} | {t.Duration} | ₹{t.Price:N0}/person | Tags: {string.Join(", ", t.Tags)}");
        return $"Royal Journeys — {tours.Count} Tour Packages:\n\n{string.Join("\n", lines)}";
    }

    [McpServerResource(UriTemplate = "destination://popular", Name = "Popular Destinations", MimeType = "text/plain")]
    [Description("Overview of all supported travel destinations.")]
    public string GetPopularDestinations()
    {
        var destinations = destinationService.GetAll();
        var lines = destinations.Select(d =>
            $"• {d.Destination} ({d.Region}) | Best: {d.PeakSeason} | Budget: ₹{d.AvgBudgetPerDayINR:N0}/day");
        return $"Royal Journeys — Destinations:\n\n{string.Join("\n", lines)}";
    }

    [McpServerResource(UriTemplate = "company://policies", Name = "Company Policies", MimeType = "text/plain")]
    [Description("Royal Journeys cancellation policy and general terms.")]
    public string GetCompanyPolicies()
    {
        var agency = policyService.GetAgencyInfo();
        var tiers = agency.CancellationPolicy.Tiers.Select(t => $"  • {t.Label}: {t.RefundPercent}% refund");
        var groupTiers = agency.GroupDiscountTiers.Select(t => $"  • {t.MinPax}–{t.MaxPax} pax: {t.DiscountPercent}% off");

        return $"""
            {agency.Name} — Policies & Terms

            CANCELLATION POLICY:
            {string.Join("\n", tiers)}
            Note: {agency.CancellationPolicy.Note}

            GROUP DISCOUNTS:
            {string.Join("\n", groupTiers)}

            CONTACT:
            Phone/WhatsApp: {agency.Contact.Whatsapp}
            Email: {agency.Contact.Email}
            Office Hours: {agency.Contact.OfficeHours}
            """;
    }
}
