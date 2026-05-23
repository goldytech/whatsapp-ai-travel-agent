namespace Waha.McpServer.Models;

public record Tour(
    string Id, string Name, string Destination, string Duration, int DurationDays,
    decimal Price, decimal SingleSupplement, string Currency,
    string[] Tags, string[] Highlights,
    string[] Inclusions, string[] Exclusions,
    Dictionary<string, int> Availability,
    DayProgram[] Itinerary,
    Review[] Reviews,
    string[] ImageUrls
);

public record DayProgram(int Day, string Title, string Activities);

public record Review(string Author, float Rating, string Comment, string Date, bool Verified);
