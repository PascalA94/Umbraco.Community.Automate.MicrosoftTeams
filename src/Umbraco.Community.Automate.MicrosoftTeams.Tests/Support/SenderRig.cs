using System.Text.Json.Nodes;
using CredentialHelperContract;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Http;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>A real sender over fakes: a recording handler, a fake clock, an in-memory credential and a capturing logger.</summary>
internal sealed class SenderRig
{
    public SenderRig(string? url = TestUrls.Sentinel, string credential = "sentinel", Action<TeamsOptions>? configure = null, string[]? extraAllowedPrefixes = null, RecordingHandler? handler = null, Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        Handler = handler ?? new RecordingHandler();
        Factory = new FakeHttpClientFactory(Handler);
        Options = new TeamsOptions();
        configure?.Invoke(Options);
        new TeamsOptionsPostConfigure().PostConfigure(null, Options);
        Monitor = new TestOptionsMonitor<TeamsOptions>(Options);
        Store = configuration is null ? TestCredentials.Store(credential, url, extraAllowedPrefixes ?? []) : TestCredentials.Store(configuration, extraAllowedPrefixes ?? []);
        Limiter = new WebhookRateLimiter(Monitor, Clock);
        Logs = new CaptureLoggerProvider();
        Sender = new TeamsWebhookSender(
            Factory,
            Monitor,
            Store,
            new WebhookUrlValidator(Monitor),
            Limiter,
            Clock,
            new LoggerFactory([Logs]).CreateLogger<TeamsWebhookSender>());
    }

    public RecordingHandler Handler { get; }

    public FakeHttpClientFactory Factory { get; }

    public TeamsOptions Options { get; }

    public TestOptionsMonitor<TeamsOptions> Monitor { get; }

    public TeamsCredentialStore Store { get; }

    public FakeTimeProvider Clock { get; } = new();

    public WebhookRateLimiter Limiter { get; }

    public CaptureLoggerProvider Logs { get; }

    public TeamsWebhookSender Sender { get; }

    public static JsonObject Envelope(string text = "hello") => CardEnvelope.Wrap(new JsonObject
    {
        ["type"] = "AdaptiveCard",
        ["version"] = "1.4",
        ["body"] = new JsonArray(new JsonObject { ["type"] = "TextBlock", ["text"] = text }),
    });

    public Task<TeamsPostResult> Post(string? credential = "sentinel", JsonObject? envelope = null, CancellationToken ct = default) =>
        Sender.PostAsync(credential, envelope ?? Envelope(), ct);
}
