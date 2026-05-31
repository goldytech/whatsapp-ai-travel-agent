namespace AgentForge.Verticals.Travel.Models;

public record Hotel(
    string Id,
    string Destination,
    string Name,
    string Tier,
    string LocationDescription,
    string Description,
    string[] ImageUrls
);
