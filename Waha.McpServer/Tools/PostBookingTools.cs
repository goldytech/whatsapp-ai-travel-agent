using System.Collections.Concurrent;
using ModelContextProtocol.Server;
using Waha.McpServer.Services;

namespace Waha.McpServer.Tools;

[McpServerToolType]
public class PostBookingTools(TourCatalogService catalog)
{
    private static readonly ConcurrentDictionary<string, (string TourId, int Rating, string? Comment, DateTime At)> _feedback = new();

    [McpServerTool(Name = "get_day_by_day_itinerary", ReadOnly = true)]
    [Description("Get the detailed day-by-day program for a tour.")]
    public string GetDayByDayItinerary(
        [Description("Tour ID")] string tourId)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found.";

        var days = tour.Itinerary.Select(d =>
            $"*Day {d.Day} – {d.Title}*\n{d.Activities}");
        return $"📅 *Itinerary — {tour.Name}* ({tour.Duration})\n\n{string.Join("\n\n", days)}";
    }

    [McpServerTool(Name = "get_departure_checklist", ReadOnly = true)]
    [Description("Get a pre-departure checklist including documents, health preparation, and day-of instructions.")]
    public string GetDepartureChecklist(
        [Description("Tour ID")] string tourId)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found.";

        return $"""
            ✅ *Pre-Departure Checklist — {tour.Name}*

            📄 *Documents:*
              • Original government-issued photo ID (Aadhar/Passport)
              • Booking confirmation from Royal Journeys
              • Flight/train tickets
              • Travel insurance policy
              • 2 passport-size photos

            💊 *Health Prep:*
              • Visit your doctor if you have any medical conditions
              • Carry prescription medicines + copy of prescription
              • Pack a basic first aid kit

            🧳 *Day Before:*
              • Confirm pickup time with your tour escort (we will WhatsApp you)
              • Charge all devices
              • Exchange some cash (ATMs may be limited at destinations)

            📞 *Emergency Contact:* Royal Journeys 24/7 — +91-98765-43210
            """;
    }

    [McpServerTool(Name = "submit_trip_feedback", ReadOnly = false)]
    [Description("Submit feedback and a star rating after completing a trip.")]
    public string SubmitTripFeedback(
        [Description("Customer's phone number")] string phone,
        [Description("Tour ID")] string tourId,
        [Description("Rating from 1 (poor) to 5 (excellent)")] int rating,
        [Description("Optional comment about the experience")] string? comment = null)
    {
        if (rating is < 1 or > 5)
            return "Rating must be between 1 and 5 stars.";

        var key = $"{phone}:{tourId}";
        _feedback[key] = (tourId, rating, comment, DateTime.UtcNow);

        var emoji = rating switch { 5 => "🌟", 4 => "⭐", 3 => "😊", 2 => "😐", _ => "😔" };
        var response = rating >= 4
            ? $"Thank you so much! {emoji} We're thrilled you had a great experience!\n\n💬 Would you like to share your experience on Google Maps? https://g.page/royaljourneys/review"
            : $"Thank you for your honest feedback. {emoji} We take this seriously and our team will reach out to understand how we can do better. Your journey matters to us! 🙏";

        return $"✅ Feedback recorded!\n\n{response}";
    }

    [McpServerTool(Name = "get_tour_reviews", ReadOnly = true)]
    [Description("Get customer reviews and average rating for a tour.")]
    public string GetTourReviews(
        [Description("Tour ID")] string tourId)
    {
        var tour = catalog.GetById(tourId);
        if (tour is null) return $"Tour '{tourId}' not found.";

        if (tour.Reviews.Length == 0)
            return $"No reviews yet for {tour.Name}. Be the first to explore and share your experience! 🌟";

        var avgRating = tour.Reviews.Average(r => r.Rating);
        var reviews = tour.Reviews.Select(r =>
            $"⭐ *{r.Rating}/5* — \"{r.Comment}\"\n— {r.Author}{(r.Verified ? " ✓ Verified" : "")}, {r.Date}");

        return $"""
            ⭐ *Reviews for {tour.Name}*
            Average Rating: *{avgRating:F1}/5* ({tour.Reviews.Length} review{(tour.Reviews.Length != 1 ? "s" : "")})

            {string.Join("\n\n", reviews)}
            """;
    }
}
