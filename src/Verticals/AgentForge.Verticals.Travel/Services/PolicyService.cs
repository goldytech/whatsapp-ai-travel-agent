using System.Text.Json;
using AgentForge.Verticals.Travel.Models;

namespace AgentForge.Verticals.Travel.Services;

public class PolicyService
{
    private readonly List<FaqTopic> _faq;
    private readonly AgencyInfo _agency;

    public PolicyService()
    {
        _faq = JsonSerializer.Deserialize<List<FaqTopic>>(TravelDataFiles.ReadAllText("FAQ.json"),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];

        _agency = JsonSerializer.Deserialize<AgencyInfo>(TravelDataFiles.ReadAllText("AgencyInfo.json"),
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
