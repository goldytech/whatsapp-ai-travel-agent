using AgentForge.Verticals.Abstractions;

namespace AgentForge.WebApi.Scheduling;

/// <summary>
/// Background service that manages timed reminders: pre-departure, course deadlines, etc.
/// In production, replace with a persistent scheduler (Quartz.NET or Hangfire).
/// </summary>
public sealed class SchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<SchedulerService> logger) : BackgroundService
{
    private sealed class ScheduledActionJob(ScheduledAction action)
    {
        public ScheduledAction Action { get; } = action;
        public bool Sent { get; set; }
    }

    private readonly List<ScheduledActionJob> _jobs = [];
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

    public void Schedule(ScheduledAction action)
    {
        lock (_jobsLock)
        {
            _jobs.Add(new ScheduledActionJob(action));
        }

        logger.LogInformation(
            "Scheduled {ActionType} for {ChatId} at {Date}",
            action.ActionType,
            action.ChatId,
            action.ScheduledAt);
    }

    public void ScheduleRange(IEnumerable<ScheduledAction> actions)
    {
        var scheduledActions = actions.Select(action => new ScheduledActionJob(action)).ToList();
        if (scheduledActions.Count == 0)
            return;

        lock (_jobsLock)
        {
            _jobs.AddRange(scheduledActions);
        }

        logger.LogInformation("Scheduled {Count} actions", scheduledActions.Count);
    }

    private async Task ProcessDueJobsAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Snapshot under lock — avoids race with ScheduleDepartureReminders / SchedulePostTripFeedback
        List<ScheduledActionJob> dueJobs;
        lock (_jobsLock)
        {
            dueJobs = _jobs.Where(j => !j.Sent && j.Action.ScheduledAt <= now).ToList();
        }

        foreach (var job in dueJobs)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var scheduledActionHandler = scope.ServiceProvider.GetRequiredService<IScheduledActionHandler>();
                await scheduledActionHandler.HandleAsync(job.Action, ct).ConfigureAwait(false);

                job.Sent = true;
                logger.LogInformation(
                    "Sent scheduled action {ActionType} to {ChatId}",
                    job.Action.ActionType,
                    job.Action.ChatId);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to send scheduled action {ActionType} to {ChatId}",
                    job.Action.ActionType,
                    job.Action.ChatId);
            }
        }
    }
}
