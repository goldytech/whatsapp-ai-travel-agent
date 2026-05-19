using System.Text.Json;
using Waha.McpServer.Models;

namespace Waha.McpServer.Services;

public class PolicyService
{
    private readonly List<FaqTopic> _faq;
    private readonly AgencyInfo _agency;

    public PolicyService(IWebHostEnvironment env)
    {
        var faqPath = Path.Combine(env.ContentRootPath, "Data", "FAQ.json");
        _faq = JsonSerializer.Deserialize<List<FaqTopic>>(File.ReadAllText(faqPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        var agencyPath = Path.Combine(env.ContentRootPath, "Data", "AgencyInfo.json");
        _agency = JsonSerializer.Deserialize<AgencyInfo>(File.ReadAllText(agencyPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public AgencyInfo GetAgencyInfo() => _agency;

    public FaqItem? FindAnswer(string topic, string? question = null)
    {
        var topicData = _faq.FirstOrDefault(t =>
            t.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase));

        if (topicData is null) return null;

        if (!string.IsNullOrWhiteSpace(question))
        {
            return topicData.Questions.FirstOrDefault(q =>
                q.Q.Contains(question, StringComparison.OrdinalIgnoreCase))
                ?? topicData.Questions.FirstOrDefault();
        }

        return topicData.Questions.FirstOrDefault();
    }

    public IReadOnlyList<FaqTopic> GetAllFaq() => _faq;
}
