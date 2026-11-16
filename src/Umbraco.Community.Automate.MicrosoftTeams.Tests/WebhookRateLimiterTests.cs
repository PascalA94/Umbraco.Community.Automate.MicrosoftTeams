using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using CredentialHelper = CredentialHelperContract.TestOptionsMonitor<Umbraco.Community.Automate.MicrosoftTeams.Configuration.TeamsOptions>;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class WebhookRateLimiterTests
{
    private static (WebhookRateLimiter Limiter, FakeTimeProvider Clock, CredentialHelper Monitor) Make()
    {
        var options = new TeamsOptions();
        new TeamsOptionsPostConfigure().PostConfigure(null, options);
        var clock = new FakeTimeProvider();
        var monitor = new CredentialHelper(options);
        return (new WebhookRateLimiter(monitor, clock), clock, monitor);
    }

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(5);

    // Advances the fake clock in small steps, letting continuations run between steps, until every task is done.
    private static async Task Pump(FakeTimeProvider clock, params Task[] tasks)
    {
        for (var i = 0; i < 2000 && !tasks.All(t => t.IsCompleted); i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(10));

            // A real pause, not a yield: under a parallel test run the continuations need a moment to record their
            // grant before the fake clock moves again, or the recorded times bunch up.
            await Task.Delay(2);
        }
    }

    [Fact]
    public async Task Burst4_ThenThreePerSecond()
    {
        var (limiter, clock, _) = Make();
        for (var i = 0; i < 4; i++)
        {
            Assert.True(await limiter.WaitAsync("a", Wait, default));
        }

        var fifth = limiter.WaitAsync("a", Wait, default).AsTask();
        await Task.Yield();
        Assert.False(fifth.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(300));
        await Task.Yield();
        Assert.False(fifth.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(40));
        Assert.True(await fifth);
    }

    [Fact]
    public async Task TwentyConcurrent_NoMoreThanTheBucketEnvelopeInAnySecond()
    {
        // A bucket of burst 4 refilling at 3 a second grants at most burst + rate * window in any window
        // (7 in a second); the plan's literal "4 in any second" can't hold for those defaults.
        var (limiter, clock, _) = Make();
        var grants = new List<TimeSpan>();
        var start = clock.GetUtcNow();
        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            if (await limiter.WaitAsync("a", Wait, default))
            {
                lock (grants)
                {
                    grants.Add(clock.GetUtcNow() - start);
                }
            }
        }).ToArray();
        await Pump(clock, tasks);
        Assert.All(tasks, t => Assert.True(t.IsCompletedSuccessfully));

        // 4 burst + 5 s x 3 a second = 19 grants fit inside the 5 s wait; the 20th was refused.
        Assert.InRange(grants.Count, 18, 19);
        var sorted = grants.OrderBy(g => g).ToList();
        foreach (var g in sorted)
        {
            var inWindow = sorted.Count(x => x >= g && x < g + TimeSpan.FromSeconds(1));
            Assert.True(inWindow <= 4 + 3, $"{inWindow} grants in one second");
        }

        Assert.True(sorted.Count(x => x < TimeSpan.FromMilliseconds(1)) <= 4);
    }

    [Fact]
    public async Task Wait_BeyondMaxWait_ReturnsFalse()
    {
        var (limiter, _, _) = Make();
        for (var i = 0; i < 4; i++)
        {
            await limiter.WaitAsync("a", Wait, default);
        }

        Assert.False(await limiter.WaitAsync("a", TimeSpan.FromMilliseconds(100), default));
    }

    [Fact]
    public async Task Wait_BoundedByCallerTimeout()
    {
        var (limiter, clock, _) = Make();
        for (var i = 0; i < 4; i++)
        {
            await limiter.WaitAsync("a", Wait, default);
        }

        using var cts = new CancellationTokenSource();
        var waiting = limiter.WaitAsync("a", Wait, cts.Token).AsTask();
        await Task.Yield();
        clock.Advance(TimeSpan.FromMilliseconds(100));
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);

        // The cancelled reservation was handed back: the next caller waits one slot, not two.
        var next = limiter.WaitAsync("a", Wait, default).AsTask();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        await Pump(clock, next);
        Assert.True(await next);
    }

    [Fact]
    public async Task SeparateCredentialNames_SeparateBuckets()
    {
        var (limiter, _, _) = Make();
        for (var i = 0; i < 4; i++)
        {
            Assert.True(await limiter.WaitAsync("a", Wait, default));
        }

        for (var i = 0; i < 4; i++)
        {
            Assert.True(await limiter.WaitAsync("b", Wait, default));
        }
    }

    [Fact]
    public async Task OptionsChange_AppliesToNewWaits()
    {
        var (limiter, clock, monitor) = Make();
        for (var i = 0; i < 4; i++)
        {
            await limiter.WaitAsync("a", Wait, default);
        }

        monitor.CurrentValue = new TeamsOptions { RateLimit = new TeamsOptions.RateLimitOptions { RequestsPerSecond = 100, Burst = 4, MaxWaitSeconds = 5 } };
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.True(await limiter.WaitAsync("a", Wait, default));
        Assert.True(await limiter.WaitAsync("a", Wait, default));
    }
}
