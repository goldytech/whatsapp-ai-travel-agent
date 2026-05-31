using AgentForge.Verticals.Abstractions;

namespace AgentForge.Verticals.Travel;

public sealed class TravelScheduledActionHandler(IMessageSender messageSender) : IScheduledActionHandler
{
    public async Task HandleAsync(ScheduledAction action, CancellationToken ct = default)
    {
        switch (action.ActionType)
        {
            case TravelScheduledActionTypes.PreDeparture7Day:
                await messageSender.SendTextAsync(
                    action.ChatId,
                    $$"""
                    ⏰ *7 Days to Go!*

                    Your *{{action.ItemName}}* trip is just 7 days away! 🎉

                    ✅ *Checklist:*
                    • Check your ID/Passport validity
                    • Pack weather-appropriate clothes
                    • Download offline maps
                    • Charge all devices
                    • Inform your bank about travel
                    """,
                    ct).ConfigureAwait(false);
                return;

            case TravelScheduledActionTypes.PreDeparture1Day:
                await messageSender.SendTextAsync(
                    action.ChatId,
                    $$"""
                    🚀 *Tomorrow is the day!*

                    *{{action.ItemName}}* — Departure tomorrow!

                    📌 Your driver will arrive at *6:00 AM*
                    📞 Driver contact: +91 98765 43210
                    🎒 Keep your documents handy
                    Have an amazing trip! ✈️
                    """,
                    ct).ConfigureAwait(false);
                return;

            case TravelScheduledActionTypes.DepartureDay:
                await messageSender.SendTextAsync(
                    action.ChatId,
                    $$"""
                    🎉 *Today's the Day! Your {{action.ItemName}} adventure begins!*

                    Safe travels and make wonderful memories! 📸
                    """,
                    ct).ConfigureAwait(false);
                return;

            case TravelScheduledActionTypes.PostTripFeedback:
                await messageSender.SendTextAsync(
                    action.ChatId,
                    $$"""
                    🎉 *Welcome back from your {{action.ItemName}} adventure!*

                    We hope you had an amazing time! Your feedback helps us improve. 🙏
                    """,
                    ct).ConfigureAwait(false);

                await messageSender.SendTextAsync(
                    action.ChatId,
                    """
                    ⭐ *We'd love your feedback!*

                    Please rate your overall experience by replying with a number:

                    1️⃣  — Poor
                    2️⃣  — Below Average
                    3️⃣  — Average
                    4️⃣  — Good
                    5️⃣  — Excellent

                    Just reply with *1*, *2*, *3*, *4*, or *5* 👆
                    """,
                    ct).ConfigureAwait(false);
                return;

            default:
                throw new InvalidOperationException($"Unsupported travel scheduled action type '{action.ActionType}'.");
        }
    }
}
