using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class SecretLeakTests
{
    public static TheoryData<string> EveryOutcome => new() { "202", "307", "400-echo", "400-bare-host", "400-json-url", "400-encoded-sig", "401", "404", "429", "500", "timeout", "http-exception" };

    private static RecordingHandler For(string outcome) => outcome switch
    {
        "202" => new RecordingHandler(),
        "307" => new RecordingHandler { Status = HttpStatusCode.TemporaryRedirect, Headers = { ["Location"] = TestUrls.Sentinel } },
        "400-echo" => new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = $"bad {TestUrls.Sentinel}" },
        "400-bare-host" => new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = $"bad {TestUrls.Sentinel["https://".Length..]}" },
        "400-json-url" => new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = "{\"error\":\"bad " + TestUrls.Sentinel.Replace("/", "\\/") + "\"}" },
        "400-encoded-sig" => new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = "bad " + TestUrls.Sentinel["https://".Length..].Replace("sig=", "SIG%3D") },
        "401" => new RecordingHandler { Status = HttpStatusCode.Unauthorized, Body = TestUrls.Sentinel },
        "404" => new RecordingHandler { Status = HttpStatusCode.NotFound, Body = TestUrls.Sentinel },
        "429" => new RecordingHandler { Status = HttpStatusCode.TooManyRequests },
        "500" => new RecordingHandler { Status = HttpStatusCode.InternalServerError, Body = TestUrls.Sentinel },
        "timeout" => new RecordingHandler { Hang = true },
        _ => new RecordingHandler { Throw = new HttpRequestException($"failed: {TestUrls.Sentinel}", new InvalidOperationException(TestUrls.Sentinel)) },
    };

    [Theory]
    [MemberData(nameof(EveryOutcome))]
    public async Task Sender_NoOutcome_LeaksUrl(string outcome)
    {
        using var telemetry = new TelemetryCapture();
        var rig = new SenderRig(configure: o => o.TimeoutSeconds = 1, handler: For(outcome));
        var post = rig.Post();
        for (var i = 0; i < 50 && !post.IsCompleted; i++)
        {
            await Task.Yield();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        var r = await post;
        var captured = rig.Logs.AllText.Concat(telemetry.Texts).Concat(r.Warnings).Concat(r.TestWarnings).Append(r.Failure?.Message ?? string.Empty);
        Assert.All(captured, t => Assert.False(TestUrls.Leaks(t), t));
    }

    [Fact]
    public async Task Sender_400EchoesValidatedHostOutsideAllowList_NeverInFailureMessage()
    {
        // StrictHosts is off, so this host is accepted with a warning and is not covered by AllowedHosts: only the validated host knows it.
        var rig = new SenderRig("https://hook-sentinelhost.example.org/SENTINELPATH?sig=SENTINELSIG", handler: new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = "rejected hook-sentinelhost.example.org/SENTINELPATH" }); // gitleaks:allow
        var r = await rig.Post();
        Assert.False(TestUrls.Leaks(r.Failure!.Message), r.Failure.Message);
        Assert.False(TestUrls.Leaks(string.Join(' ', rig.Logs.AllText)));
    }

    [Fact]
    public async Task Sender_SentinelHost_NeverInRunTimeOutput()
    {
        var rig = new SenderRig("https://SENTINELSIG.example.org/p?x=1");
        var r = await rig.Post();
        var text = string.Join(' ', rig.Logs.AllText.Concat(r.Warnings).Append(r.Failure?.Message ?? string.Empty));
        Assert.DoesNotContain("SENTINEL", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FrameworkHttpLogging_AtTrace_NeverContainsSig()
    {
        // A real IHttpClientFactory on Automate's NoRedirect client name, so the framework's own logging handlers run.
        var logs = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(logs));
        services.AddHttpClient(TeamsWebhookSender.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => new RecordingHandler());
        using var provider = services.BuildServiceProvider();
        var rig = new SenderRig();
        var sender = new TeamsWebhookSender(
            provider.GetRequiredService<IHttpClientFactory>(),
            rig.Monitor,
            rig.Store,
            new WebhookUrlValidator(rig.Monitor),
            rig.Limiter,
            rig.Clock,
            provider.GetRequiredService<ILoggerFactory>().CreateLogger<TeamsWebhookSender>());
        var r = await sender.PostAsync("sentinel", SenderRig.Envelope(), default);
        Assert.True(r.Accepted);
        Assert.NotEmpty(logs.Entries);
        Assert.DoesNotContain(logs.AllText, t => t.Contains("SENTINELSIG", StringComparison.Ordinal));

        // Known residual (spike S4): the framework logs the host and path (not the query) of the client's requests. Asserted
        // so a change in either direction is noticed; the package never logs them itself.
        var framework = string.Join(' ', logs.Entries.Where(e => e.Category.StartsWith("System.Net.Http", StringComparison.Ordinal)).Select(e => e.Text));
        Assert.Contains("powerplatform.com", framework, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiagnosticListenerPayloadObject_HoldsRequest_KnownResidualR2()
    {
        // Real sockets to a name that can't resolve: the diagnostic events fire before the connection fails.
        using var telemetry = new TelemetryCapture();
        using var client = new HttpClient(new HttpClientHandler());
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => client.PostAsync(
            "https://sentinel-host.invalid/p?sig=SENTINELSIG",
            new StringContent("{}", Encoding.UTF8, "application/json")));

        // Documents the limit (F07): the payload object carries the request itself, so any subscriber can read the full
        // URI. A future runtime change to this is noticed. Not a leak test.
        Assert.Contains(telemetry.Payloads, p => p.GetType().GetProperty("Request")?.GetValue(p) is HttpRequestMessage);
    }
}
