using System.Text.Json;
using AgentForge.Verticals.Travel.Models;

namespace AgentForge.Verticals.Travel.Services;

public class DestinationService
{
    private readonly List<DestinationGuide> _guides;

    public DestinationService()
    {
        var json = TravelDataFiles.ReadAllText("DestinationGuide.json");
        _guides = JsonSerializer.Deserialize<List<DestinationGuide>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public DestinationGuide? GetByDestination(string destination) =>
        _guides.FirstOrDefault(d => d.Destination.Contains(destination, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<DestinationGuide> GetAll() => _guides;
}
