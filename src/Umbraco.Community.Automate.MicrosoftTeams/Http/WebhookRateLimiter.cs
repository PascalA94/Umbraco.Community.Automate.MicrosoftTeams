using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>
/// One token bucket per normalised credential name, shared by every action that posts to that webhook (F01). Our own
/// bucket over <see cref="TimeProvider"/>, because the BCL limiter can't take a fake clock. A caller reserves a token
/// and waits for its slot, so concurrent callers are spaced out instead of all waking at once.
/// </summary>
internal sealed class WebhookRateLimiter(IOptionsMonitor<TeamsOptions> options, TimeProvider timeProvider)
{
    // Instance field, never static. The key set is bounded by the configured credential names.
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.Ordinal);

    /// <summary>True when a slot was granted within <paramref name="maxWait"/>; false when none was. Caller cancellation throws.</summary>
    public async ValueTask<bool> WaitAsync(string normalisedCredentialName, TimeSpan maxWait, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var limits = options.CurrentValue.RateLimit;
        var bucket = _buckets.GetOrAdd(normalisedCredentialName, _ => new Bucket(limits.Burst, timeProvider.GetTimestamp()));

        TimeSpan wait;
        lock (bucket)
        {
            var now = timeProvider.GetTimestamp();
            var elapsed = timeProvider.GetElapsedTime(bucket.LastRefill, now).TotalSeconds;
            bucket.LastRefill = now;
            bucket.Tokens = Math.Min(limits.Burst, bucket.Tokens + elapsed * limits.RequestsPerSecond);

            // Reserve a token. A negative balance is the queue of callers already waiting for theirs.
            bucket.Tokens -= 1;
            if (bucket.Tokens >= 0)
            {
                return true;
            }

            wait = TimeSpan.FromSeconds(-bucket.Tokens / limits.RequestsPerSecond);
            if (wait > maxWait)
            {
                bucket.Tokens += 1;
                return false;
            }
        }

        try
        {
            await Task.Delay(wait, timeProvider, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            lock (bucket)
            {
                bucket.Tokens += 1;
            }

            throw;
        }
    }

    private sealed class Bucket(double tokens, long lastRefill)
    {
        public double Tokens { get; set; } = tokens;

        public long LastRefill { get; set; } = lastRefill;
    }
}
