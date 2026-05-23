using System.Text.Json;
using Waha.McpServer.Models;

namespace Waha.McpServer.Services;

public class HotelService
{
    private readonly List<Hotel> _hotels;

    public HotelService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "Hotels.json");
        var json = File.ReadAllText(path);
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
