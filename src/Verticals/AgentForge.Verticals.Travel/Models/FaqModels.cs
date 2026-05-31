namespace AgentForge.Verticals.Travel.Models;

public record FaqTopic(string Topic, FaqItem[] Questions);
public record FaqItem(string Q, string A);
