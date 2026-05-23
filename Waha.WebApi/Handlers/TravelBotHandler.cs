namespace Waha.WebApi.Handlers;

// Handles scheduled WhatsApp notifications (departure reminders, post-trip feedback).
// Tour inquiry logic has moved to the MCP server tools used by the AI agent.
public sealed class TravelBotHandler(WahaApiClient wahaClient)
{
    public Task SendEchoAsync(string chatId, string text, CancellationToken ct = default) =>
        wahaClient.SendTextAsync(chatId, text, ct: ct);

    public async Task SendDepartureReminderAsync(string chatId, string tourName, int daysUntilDeparture, CancellationToken ct = default)
    {
        var message = daysUntilDeparture switch
        {
            7 => $"⏰ *7 Days to Go!*\n\nYour *{tourName}* trip is just 7 days away! 🎉\n\n" +
                 "✅ *Checklist:*\n• Check your ID/Passport validity\n• Pack weather-appropriate clothes\n" +
                 "• Download offline maps\n• Charge all devices\n• Inform your bank about travel",
            1 => $"🚀 *Tomorrow is the day!*\n\n*{tourName}* — Departure tomorrow!\n\n" +
                 "📌 Your driver will arrive at *6:00 AM*\n" +
                 "📞 Driver contact: +91 98765 43210\n" +
                 "🎒 Keep your documents handy\n" +
                 "Have an amazing trip! ✈️",
            0 => $"🎉 *Today's the Day! Your {tourName} adventure begins!*\n\n" +
                 "Safe travels and make wonderful memories! 📸",
            _ => $"⏰ *{daysUntilDeparture} days to your {tourName} trip!* Are you excited? 🎒"
        };

        await wahaClient.SendTextAsync(chatId, message, ct: ct).ConfigureAwait(false);
    }
}
