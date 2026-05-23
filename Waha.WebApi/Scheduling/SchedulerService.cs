namespace Waha.WebApi.Scheduling;

/// <summary>
/// Background service that manages timed reminders: pre-departure, course deadlines, etc.
/// In production, replace with a persistent scheduler (Quartz.NET or Hangfire).
/// </summary>
public sealed class SchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<SchedulerService> logger) : BackgroundService
{
    private readonly List<ReminderJob> _jobs = [];
    // System.Threading.Lock (C# 14 / .NET 10+) — higher-perf than locking on `object`
    private readonly Lock _jobsLock = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessDueJobsAsync(stoppingToken).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
    }

    public void ScheduleDepartureReminders(string chatId, string tourName, DateTimeOffset departureDate)
    {
        lock (_jobsLock)
        {
            _jobs.AddRange(
            [
                new ReminderJob(chatId, ReminderType.PreDeparture7Day, tourName, departureDate.AddDays(-7)),
                new ReminderJob(chatId, ReminderType.PreDeparture1Day, tourName, departureDate.AddDays(-1)),
                new ReminderJob(chatId, ReminderType.DepartureDay, tourName, departureDate),
            ]);
        }

        logger.LogInformation("Scheduled 3 departure reminders for {ChatId} — {TourName} on {Date}",
            chatId, tourName, departureDate);
    }

    public void SchedulePostTripFeedback(string chatId, string tripName, DateTimeOffset returnDate)
    {
        lock (_jobsLock)
        {
            _jobs.Add(new ReminderJob(chatId, ReminderType.PostTripFeedback, tripName,
                returnDate.AddHours(24)));
        }

        logger.LogInformation("Scheduled post-trip feedback for {ChatId} — {TripName}", chatId, tripName);
    }

    private async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Snapshot under lock — avoids race with ScheduleDepartureReminders / SchedulePostTripFeedback
        List<ReminderJob> dueJobs;
        lock (_jobsLock)
        {
            dueJobs = _jobs.Where(j => !j.Sent && j.ScheduledAt <= now).ToList();
        }

        foreach (var job in dueJobs)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var wahaClient = scope.ServiceProvider.GetRequiredService<WahaApiClient>();
                var travelBot = scope.ServiceProvider.GetRequiredService<TravelBotHandler>();
                var feedbackHandler = scope.ServiceProvider.GetRequiredService<FeedbackHandler>();

                switch (job.Type)
                {
                    case ReminderType.PreDeparture7Day:
                        await travelBot.SendDepartureReminderAsync(job.ChatId, job.ItemName, 7, ct).ConfigureAwait(false);
                        break;

                    case ReminderType.PreDeparture1Day:
                        await travelBot.SendDepartureReminderAsync(job.ChatId, job.ItemName, 1, ct).ConfigureAwait(false);
                        break;

                    case ReminderType.DepartureDay:
                        await travelBot.SendDepartureReminderAsync(job.ChatId, job.ItemName, 0, ct).ConfigureAwait(false);
                        break;

                    case ReminderType.PostTripFeedback:
                        await feedbackHandler.SendPostTripFeedbackAsync(job.ChatId, job.ItemName, ct).ConfigureAwait(false);
                        break;

                }

                job.Sent = true;
                logger.LogInformation("Sent {Type} reminder to {ChatId}", job.Type, job.ChatId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send {Type} reminder to {ChatId}", job.Type, job.ChatId);
            }
        }
    }
}

public enum ReminderType
{
    PreDeparture7Day,
    PreDeparture1Day,
    DepartureDay,
    PostTripFeedback
}

public sealed class ReminderJob(
    string chatId,
    ReminderType type,
    string itemName,
    DateTimeOffset scheduledAt)
{
    public string ChatId { get; } = chatId;
    public ReminderType Type { get; } = type;
    public string ItemName { get; } = itemName;
    public DateTimeOffset ScheduledAt { get; } = scheduledAt;
    public bool Sent { get; set; }
}
