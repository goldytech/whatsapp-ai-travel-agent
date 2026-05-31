using System.Text.Json;
using AgentForge.Verticals.Travel.Models;

namespace AgentForge.Verticals.Travel.Services;

public class HotelService
{
    private readonly List<Hotel> _hotels;

    public HotelService()
    {
        var json = TravelDataFiles.ReadAllText("Hotels.json");
        _hotels = JsonSerializer.Deserialize<List<Hotel>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public IReadOnlyList<Hotel> GetByDestination(string destination) =>
        _hotels
            .Where(h => h.Destination.Contains(destination, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public IReadOnlyList<string> GetSupportedDestinations() =>
        _hotels.Select(h => h.Destination).Distinct().Order().ToList();
}
