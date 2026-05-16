namespace Waha.McpServer.Services;

using System.Text.Json;
using Waha.McpServer.Models;

public class TourCatalogService
{
    private readonly List<Tour> _tours;

    public TourCatalogService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "TourCatalog.json");
        var json = File.ReadAllText(path);
        _tours = JsonSerializer.Deserialize<List<Tour>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public IReadOnlyList<Tour> GetAll() => _tours;

    public Tour? GetById(string id) =>
        _tours.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<Tour> Search(string? destination = null, string? query = null,
        decimal? maxBudget = null, int? durationDays = null, string? month = null)
    {
        var results = _tours.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(destination))
            results = results.Where(t => t.Destination.Contains(destination, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(query))
            results = results.Where(t =>
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Destination.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                t.Highlights.Any(h => h.Contains(query, StringComparison.OrdinalIgnoreCase)));

        if (maxBudget.HasValue)
            results = results.Where(t => t.Price <= maxBudget.Value);

        if (durationDays.HasValue)
            results = results.Where(t => t.DurationDays <= durationDays.Value);

        if (!string.IsNullOrWhiteSpace(month))
            results = results.Where(t =>
                t.Tags.Any(tag => tag.Contains(month, StringComparison.OrdinalIgnoreCase)) ||
                t.Availability.ContainsKey(month));

        return results.ToList();
    }
}
