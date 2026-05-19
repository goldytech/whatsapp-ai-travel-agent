using System.Text.Json;
using Waha.McpServer.Models;

namespace Waha.McpServer.Services;

public class DestinationService
{
    private readonly List<DestinationGuide> _guides;

    public DestinationService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "DestinationGuide.json");
        var json = File.ReadAllText(path);
        _guides = JsonSerializer.Deserialize<List<DestinationGuide>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public DestinationGuide? GetByDestination(string destination) =>
        _guides.FirstOrDefault(d => d.Destination.Contains(destination, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<DestinationGuide> GetAll() => _guides;
}
