using System.Collections.Concurrent;

namespace AgentForge.WebApi.Services;

public sealed class OpenWaWebhookIdempotencyStore
{
    private static readonly TimeSpan Retention = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);
    private long _nextCleanupTicks = DateTimeOffset.UtcNow.Add(CleanupInterval).UtcTicks;

    public bool TryRegister(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        CleanupExpired(now);

        return _entries.TryAdd(key, now.Add(Retention));
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        if (now.UtcTicks < Interlocked.Read(ref _nextCleanupTicks))
        {
            return;
        }

        foreach (var entry in _entries)
        {
            if (entry.Value <= now)
            {
                _entries.TryRemove(entry.Key, out _);
            }
        }

        Interlocked.Exchange(ref _nextCleanupTicks, now.Add(CleanupInterval).UtcTicks);
    }
}
