using System.Collections.Concurrent;

namespace Waha.WebApi.Handlers;

public sealed class FeedbackHandler(
    WahaApiClient wahaClient,
    ILogger<FeedbackHandler> logger)
{
    private const string GoogleReviewLink = "https://g.page/r/your-google-review-link";

    // Tracks chatIds that have been shown the rating menu and are awaiting a 1-5 reply.
    // WAHA free (WEBJS) does not support native polls, so we use a text-based numbered menu.
    private readonly ConcurrentDictionary<string, bool> _pendingFeedback = new();

    public bool IsPendingFeedback(string chatId) => _pendingFeedback.TryGetValue(chatId, out _);

    public void ClearPendingFeedback(string chatId) => _pendingFeedback.TryRemove(chatId, out _);

    public async Task SendFeedbackPollAsync(string chatId, CancellationToken ct = default)
    {
        _pendingFeedback[chatId] = true;

        await wahaClient.SendTextAsync(chatId,
            "⭐ *We'd love your feedback!*\n\n" +
            "Please rate your overall experience by replying with a number:\n\n" +
            "1️⃣  — Poor\n" +
            "2️⃣  — Below Average\n" +
            "3️⃣  — Average\n" +
            "4️⃣  — Good\n" +
            "5️⃣  — Excellent\n\n" +
            "Just reply with *1*, *2*, *3*, *4*, or *5* 👆",
            ct: ct).ConfigureAwait(false);
    }

    public async Task HandleRatingResponseAsync(string chatId, string ratingText, CancellationToken ct = default)
    {
        _pendingFeedback.TryRemove(chatId, out _);

        if (!int.TryParse(ratingText.Trim(), out var rating) || rating < 1 || rating > 5)
        {
            await wahaClient.SendTextAsync(chatId,
                "Please reply with a number between *1* and *5* to rate your experience. 🙏",
                ct: ct).ConfigureAwait(false);
            return;
        }

        logger.LogInformation("Feedback rating from {ChatId}: {Rating}/5", chatId, rating);
        await SendPostVoteFollowUpAsync(chatId, rating, ct).ConfigureAwait(false);
    }

    public async Task SendPostTripFeedbackAsync(string chatId, string tripName, CancellationToken ct = default)
    {
        await wahaClient.SendTextAsync(chatId,
            $"🎉 *Welcome back from your {tripName} adventure!*\n\n" +
            "We hope you had an amazing time! Your feedback helps us improve. 🙏",
            ct: ct).ConfigureAwait(false);

        await SendFeedbackPollAsync(chatId, ct).ConfigureAwait(false);
    }

    public async Task HandlePollVoteAsync(WahaWebhookPayload payload, CancellationToken ct = default)
    {
        if (payload.Payload is null)
            return;

        var vote = payload.Payload.Value.Deserialize<WahaPollVote>(JsonSerializerOptions.Web);
        if (vote is null)
            return;

        var selectedOption = vote.SelectedOptions.FirstOrDefault() ?? string.Empty;
        logger.LogInformation("Poll vote from {ChatId}: {Option}", vote.ChatId, selectedOption);

        // Extract rating from option text (e.g. "⭐⭐⭐⭐⭐ 5 - Excellent" → 5)
        var rating = selectedOption.Contains("5") ? 5
                   : selectedOption.Contains("4") ? 4
                   : selectedOption.Contains("3") ? 3
                   : selectedOption.Contains("2") ? 2
                   : 1;

        await SendPostVoteFollowUpAsync(vote.ChatId, rating, ct).ConfigureAwait(false);
    }

    private async Task SendPostVoteFollowUpAsync(string chatId, int rating, CancellationToken ct)
    {
        if (rating >= 4)
        {
            await wahaClient.SendTextAsync(chatId,
                $"🌟 *Thank you for your {rating}-star rating!*\n\n" +
                "We're thrilled you had a great experience! Would you consider leaving us a Google review? " +
                "It takes just 1 minute and helps others discover us:\n\n" +
                $"👉 {GoogleReviewLink}",
                ct: ct).ConfigureAwait(false);
        }
        else
        {
            await wahaClient.SendTextAsync(chatId,
                "😔 *Thank you for your honest feedback.*\n\n" +
                "We're sorry your experience wasn't perfect. Our team will reach out to you shortly to understand " +
                "how we can do better.\n\n" +
                "📞 You can also reach us directly: +91 98765 43210",
                ct: ct).ConfigureAwait(false);
        }
    }
}
