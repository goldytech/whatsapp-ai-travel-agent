using ModelContextProtocol.Server;
using Waha.McpServer.Services;

namespace Waha.McpServer.Tools;

[McpServerToolType]
public class HotelTools(HotelService hotelService)
{
    [McpServerTool(Name = "get_hotels_by_destination", ReadOnly = true)]
    [Description("Get curated hotel recommendations for a destination, organised by tier (Budget / Mid-range / Luxury). Returns hotel names, descriptions, and image markers for visual preview.")]
    public string GetHotelsByDestination(
        [Description("Destination name (e.g. 'Goa', 'Kerala', 'Manali', 'Rajasthan', 'Andaman Islands')")] string destination)
    {
        var hotels = hotelService.GetByDestination(destination);

        if (hotels.Count == 0)
            return $"No curated hotel recommendations available for '{destination}'. " +
                   $"Supported destinations: {string.Join(", ", hotelService.GetSupportedDestinations())}.";

        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"🏨 *Recommended Hotels — {destination}*");
        lines.AppendLine();

        foreach (var hotel in hotels.OrderBy(h => TierOrder(h.Tier)))
        {
            lines.AppendLine($"*{TierEmoji(hotel.Tier)} {hotel.Tier}: {hotel.Name}*");
            lines.AppendLine($"📍 {hotel.LocationDescription}");
            lines.AppendLine(hotel.Description);

            if (!string.IsNullOrWhiteSpace(hotel.ImageUrl))
                lines.AppendLine($"{{{{image:{hotel.ImageUrl}|{hotel.Name} ({hotel.Tier})}}}}");

            lines.AppendLine();
        }

        lines.AppendLine("_Final hotel allocation depends on availability and room type at the time of booking._");

        return lines.ToString().TrimEnd();
    }

    private static int TierOrder(string tier) => tier switch
    {
        "Budget" => 0,
        "Mid-range" => 1,
        "Luxury" => 2,
        _ => 3
    };

    private static string TierEmoji(string tier) => tier switch
    {
        "Budget" => "💚",
        "Mid-range" => "💛",
        "Luxury" => "💎",
        _ => "🏨"
    };
}
