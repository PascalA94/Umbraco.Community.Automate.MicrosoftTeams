using System.Net;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class TeamsWebhookSenderTests
{
    private const string UrlNameMessage = "This looks like a webhook URL. It may have been saved on the connection: clear the field and rotate the Workflow URL (see SECURITY.md#rotation).";

    private const string Guard = "Refused: Umbraco:Automate:AllowedConfigurationKeyPrefixes contains 'Umbraco:Automate', which exposes Umbraco:Automate:MicrosoftTeams:Credentials. Remove that entry.";

    [Fact]
    public async Task Post_202_Accepted_RecordsJsonContentTypeAndBytes()
    {
        var rig = new SenderRig();
        var envelope = SenderRig.Envelope("héllo");
        var r = await rig.Post(envelope: envelope);
        Assert.True(r.Accepted);
        Assert.Equal(202, r.StatusCode);
        Assert.Null(r.Failure);
        var request = Assert.Single(rig.Handler.Requests);
        Assert.Equal("application/json; charset=utf-8", request.ContentType);
        Assert.Equal(Encoding.UTF8.GetByteCount(request.Body), r.PayloadBytes);
    }

    [Fact]
    public async Task Post_PostsOnlyToResolvedCredentialUrl()
    {
        var rig = new SenderRig();
        await rig.Post();
        Assert.Equal(new Uri(TestUrls.Sentinel).AbsoluteUri, Assert.Single(rig.Handler.Requests).Uri);
    }

    [Fact]
    public async Task Post_MissingCredential_NoRequest_ConfigurationError()
    {
        var rig = new SenderRig();
        var r = await rig.Post("sentinel2");
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Equal("No value is configured at Umbraco:Automate:MicrosoftTeams:Credentials:sentinel2:WebhookUrl.", r.Failure.Message);
        Assert.Empty(rig.Handler.Requests);
    }

    [Theory]
    [InlineData("ops-alerts")]
    [InlineData(null)]
    [InlineData("a:b")]
    public async Task Post_BadCredentialName_NoRequest_ConfigurationError(string? name)
    {
        var rig = new SenderRig();
        var r = await rig.Post(name);
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Equal("Credential name must be 1 to 32 letters or digits.", r.Failure.Message);
        Assert.Empty(rig.Handler.Requests);
    }

    public static TheoryData<string> UrlLikeNames => new()
    {
        TestUrls.Sentinel,
        "https://x.example/a",
        "ftp://SENTINELPATH",
        "alerts?SIG=SENTINELSIG", // gitleaks:allow
        "sig%3DSENTINELSIG", // gitleaks:allow
        "SENTINELPATH" + new string('a', 40),
    };

    [Theory]
    [MemberData(nameof(UrlLikeNames))]
    public async Task Post_UrlPastedAsCredentialName_FixedMessage_NoRequest_NoEcho(string name)
    {
        var rig = new SenderRig();
        var r = await rig.Post(name);
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Equal(UrlNameMessage, r.Failure.Message);
        Assert.Empty(rig.Handler.Requests);
        Assert.All(rig.Logs.AllText.Append(r.Failure.Message).Concat(r.Warnings).Concat(r.TestWarnings), t => Assert.False(TestUrls.Leaks(t), t));
        Assert.DoesNotContain("SENTINEL", r.Failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_GuardTripped_NoRequest_ConfigurationError()
    {
        var rig = new SenderRig(extraAllowedPrefixes: ["Umbraco:Automate"]);
        var r = await rig.Post();
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Equal(Guard, r.Failure.Message);
        Assert.Empty(rig.Handler.Requests);
    }

    [Theory]
    [InlineData("http://x.example/a")]
    [InlineData("https://acme.webhook.office.com/x")]
    [InlineData("not a url")]
    public async Task Post_BlockedUrl_NoRequest_ConfigurationError(string url)
    {
        var rig = new SenderRig(url);
        var r = await rig.Post();
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Post_UnknownHost_SendsAndAddsWarningWithoutHost()
    {
        var rig = new SenderRig("https://hooks.example.org/p?sig=S");
        var r = await rig.Post();
        Assert.True(r.Accepted);
        Assert.Equal(["The webhook host isn't a known Power Automate host."], r.Warnings);
        Assert.Equal(["hooks.example.org isn't a known Power Automate host."], r.TestWarnings);
    }

    [Fact]
    public async Task Post_UnknownHost_StrictHosts_NoRequest()
    {
        var rig = new SenderRig("https://hooks.example.org/p?sig=S", configure: o => o.StrictHosts = true);
        var r = await rig.Post();
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Post_OverMaxPayloadBytes_NoRequest_Validation()
    {
        var rig = new SenderRig();
        var r = await rig.Post(envelope: SenderRig.Envelope(new string('x', 29_000)));
        Assert.Equal(StepRunErrorCategory.Validation, r.Failure!.Category);
        Assert.StartsWith("Card is ", r.Failure.Message);
        Assert.EndsWith(" bytes. Teams allows about 28 KB.", r.Failure.Message);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Post_RateLimiterNoSlot_NoRequest_RateLimiting()
    {
        var rig = new SenderRig(configure: o => o.RateLimit.MaxWaitSeconds = 0);
        for (var i = 0; i < 4; i++)
        {
            Assert.True((await rig.Post()).Accepted);
        }

        var r = await rig.Post();
        Assert.Equal(StepRunErrorCategory.RateLimiting, r.Failure!.Category);
        Assert.Equal("Too many posts to this Teams webhook at once. Use the step's Retry setting.", r.Failure.Message);
        Assert.Equal(4, rig.Handler.Requests.Count);
    }

    [Fact]
    public async Task Post_Timeout_IsTimeoutCategory()
    {
        var rig = new SenderRig(configure: o => o.TimeoutSeconds = 1, handler: new RecordingHandler { Hang = true });
        var post = rig.Post();
        for (var i = 0; i < 50 && !post.IsCompleted; i++)
        {
            await Task.Yield();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(100));
        }

        var r = await post;
        Assert.Equal(StepRunErrorCategory.Timeout, r.Failure!.Category);
        Assert.Equal("Teams didn't answer within 1 seconds.", r.Failure.Message);
    }

    [Fact]
    public async Task Post_CallerCancelled_ThrowsOperationCanceled()
    {
        var rig = new SenderRig(handler: new RecordingHandler { Hang = true });
        using var cts = new CancellationTokenSource();
        var post = rig.Post(ct: cts.Token);
        await Task.Yield();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => post);
    }

    [Fact]
    public async Task Post_HttpRequestException_ServiceUnavailable()
    {
        var rig = new SenderRig(handler: new RecordingHandler { Throw = new HttpRequestException($"connection failed to {TestUrls.Sentinel}") });
        var r = await rig.Post();
        Assert.Equal(StepRunErrorCategory.ServiceUnavailable, r.Failure!.Category);
        Assert.Equal("Couldn't reach Teams.", r.Failure.Message);
    }

    [Theory]
    [InlineData("https://127.0.0.1/p?sig=S")]
    [InlineData("https://169.254.169.254/p?sig=S")]
    public async Task Post_SsrfBlock_ConfigurationError(string url)
    {
        // Automate's own SSRF handler (internal, so built by reflection) as the primary handler, over real sockets.
        var asm = typeof(Umbraco.Automate.Core.Constants).Assembly;
        var create = asm.GetType("Umbraco.Automate.Core.Security.SsrfProtectionHandler")!.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)!;
        var handler = (HttpMessageHandler)create.Invoke(null, [false, false])!;
        var rig = new SenderRig(url, handler: null);
        var sender = new TeamsWebhookSender(
            new FakeHttpClientFactory(handler),
            rig.Monitor,
            rig.Store,
            new WebhookUrlValidator(rig.Monitor),
            rig.Limiter,
            rig.Clock,
            new LoggerFactory([rig.Logs]).CreateLogger<TeamsWebhookSender>());
        var r = await sender.PostAsync("sentinel", SenderRig.Envelope(), default);
        Assert.Equal(StepRunErrorCategory.ConfigurationError, r.Failure!.Category);
        Assert.Equal("The webhook host isn't allowed.", r.Failure.Message);
    }

    [Fact]
    public async Task Post_Redirect307_NoSecondRequest_InvalidResponse_NoLocation()
    {
        var handler = new RecordingHandler { Status = HttpStatusCode.TemporaryRedirect };
        handler.Headers["Location"] = "https://evil.example/steal";
        var rig = new SenderRig(handler: handler);
        var r = await rig.Post();
        Assert.Equal(StepRunErrorCategory.InvalidResponse, r.Failure!.Category);
        Assert.DoesNotContain("evil", r.Failure.Message);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Post_UsesTheNoRedirectClient()
    {
        var rig = new SenderRig();
        await rig.Post();
        Assert.Equal([Umbraco.Automate.Core.Constants.HttpClients.NoRedirect], rig.Factory.RequestedNames);
    }

    [Fact]
    public async Task Post_50MbBody_ReadsAtMost1024Bytes()
    {
        var body = new CountingStream(50L * 1024 * 1024);
        var rig = new SenderRig(handler: new RecordingHandler { Status = HttpStatusCode.BadRequest, BodyStream = body });

        // Intermittent on CI (ServiceUnavailable instead of InvalidResponse, seen twice on 2026-11-25, never locally).
        // The sender's catch-all hides the exception on purpose, so record the first one thrown while this sender runs:
        // type and top frames only, never the message. Remove once the cause is found and fixed.
        string? firstThrown = null;
        void Capture(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
        {
            if (firstThrown is null && Environment.StackTrace.Contains(nameof(TeamsWebhookSender), StringComparison.Ordinal))
            {
                firstThrown = e.Exception.GetType().FullName + " at " + string.Join(" | ", (e.Exception.StackTrace ?? string.Empty).Split('\n').Take(3).Select(l => l.Trim()));
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += Capture;
        TeamsPostResult r;
        try
        {
            r = await rig.Post();
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Capture;
        }

        Assert.True(r.Failure!.Category == StepRunErrorCategory.InvalidResponse, $"category {r.Failure.Category}; first exception in the sender: {firstThrown ?? "none seen"}");
        Assert.True(body.BytesRead <= 1024, $"read {body.BytesRead}");
    }

    [Fact]
    public async Task Post_BodyWithBidiAndEmail_StrippedInMessage_NeverLogged()
    {
        var rig = new SenderRig(handler: new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = "\u202Ebad card for ann@example.org" });
        var r = await rig.Post();
        Assert.DoesNotContain("\u202E", r.Failure!.Message, StringComparison.Ordinal);
        Assert.Contains("bad card", r.Failure.Message);
        Assert.DoesNotContain(rig.Logs.AllText, t => t.Contains("bad card", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Post_LogsStatusAndBytesOnly()
    {
        var rig = new SenderRig();
        var r = await rig.Post();
        var line = Assert.Single(rig.Logs.Entries, e => e.Level == LogLevel.Information);
        Assert.StartsWith($"Teams webhook returned 202 ({r.PayloadBytes} bytes)", line.Text);
        Assert.DoesNotContain("powerplatform", string.Join(' ', rig.Logs.AllText), StringComparison.OrdinalIgnoreCase);
        Assert.Single(rig.Logs.Entries);
    }

    [Fact]
    public void ValidatedWebhookUrl_AsStructuredLogArgument_NoHost()
    {
        var rig = new SenderRig();
        var validated = new WebhookUrlValidator(rig.Monitor).Check(TestUrls.Sentinel).Url!;
        new LoggerFactory([rig.Logs]).CreateLogger("t").LogInformation("{Url}", validated);
        var text = string.Join(' ', rig.Logs.AllText);
        Assert.Contains("[webhook URL]", text);
        Assert.DoesNotContain("powerplatform", text, StringComparison.OrdinalIgnoreCase);
        Assert.False(TestUrls.Leaks(text));
    }
}
