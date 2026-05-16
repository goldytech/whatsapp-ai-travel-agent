namespace Waha.McpServer.Services;

using System.Text.Json;
using Waha.McpServer.Models;

public class PromotionService
{
    private readonly List<Promotion> _promotions;

    public PromotionService(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "Promotions.json");
        var json = File.ReadAllText(path);
        _promotions = JsonSerializer.Deserialize<List<Promotion>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    public IReadOnlyList<Promotion> GetActivePromotions()
    {
        var today = DateTime.UtcNow.Date;
        return _promotions
            .Where(p => DateTime.Parse(p.ValidFrom).Date <= today && DateTime.Parse(p.ValidUntil).Date >= today)
            .ToList();
    }

    public IReadOnlyList<Promotion> GetAll() => _promotions;
}
