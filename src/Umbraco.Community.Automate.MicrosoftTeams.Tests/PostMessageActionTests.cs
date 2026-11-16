using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Testing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Connection;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class PostMessageActionTests
{
    private static ActionTestHarness<PostMessageAction> Harness(SenderRig rig, PostMessageSettings settings, string? credential = "sentinel", string? appUrl = "https://www.example.com")
    {
        var routing = Substitute.For<IOptionsMonitor<WebRoutingSettings>>();
        routing.CurrentValue.Returns(new WebRoutingSettings { UmbracoApplicationUrl = appUrl });
        var domains = Substitute.For<IDomainService>();
        domains.GetAllAsync(false).Returns(Task.FromResult<IEnumerable<IDomain>>([]));

        // The harness builds its own provider (and scope factory) from what is registered here.
        return ActionTestHarness.For<PostMessageAction>()
            .WithService<IEditableModelResolver>(new EditableModelResolverStub())
            .WithService<ITeamsWebhookSender>(rig.Sender)
            .WithService<IOptionsMonitor<Configuration.TeamsOptions>>(rig.Monitor)
            .WithService(new LinkHostPolicy(new SiteHostProvider(routing, domains, TimeProvider.System), rig.Monitor))
            .WithSettings(settings)
            .WithConnection(new TeamsWorkflowConnectionSettings { CredentialName = credential });
    }

    // Runs the step while advancing the rig's fake clock, so timeouts and limiter waits finish.
    private static async Task<ActionResult> Run(SenderRig rig, ActionTestHarness<PostMessageAction> harness)
    {
        var task = harness.ExecuteAsync();
        for (var i = 0; i < 400 && !task.IsCompleted; i++)
        {
            await Task.Yield();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        }

        return await task;
    }

    private static JsonElement Sent(SenderRig rig) => JsonDocument.Parse(Assert.Single(rig.Handler.Requests).Body).RootElement;

    private static string AllText(SenderRig rig) => string.Join('\n', rig.Handler.Requests.Select(r => r.Body));

    [Fact]
    public async Task Execute_BoundNameWithQuotesBackslashBrace_ValidJson_TextExact()
    {
        var rig = new SenderRig();
        var name = "Rock \"n\" Roll \\ }";
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "{{name}}", Data = [new CardDataRow { Key = "name", Value = name }] }));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        var text = Sent(rig).GetProperty("attachments")[0].GetProperty("content").GetProperty("body")[0].GetProperty("items")[0].GetProperty("text").GetString();
        Assert.Equal("Rock \"n\" Roll \\\\ }", text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Execute_BlankBoundMessage_PostsPlaceholder(string message)
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = message }));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.Contains("no message text", AllText(rig)); // the parentheses are escaped like any plain text
        Assert.Contains("The message was empty, so a placeholder was sent.", ((TeamsPostOutput)result.OutputData!).Warnings);
    }

    [Fact]
    public async Task Save_BlankMessageField_Error()
    {
        var action = new PostMessageAction(new ActionInfrastructure(new EditableModelResolverStub()), new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        Assert.Equal(["Message is required."], await action.ValidateSettingsAsync(new PostMessageSettings { Message = " " }, default));
        Assert.Empty(await action.ValidateSettingsAsync(new PostMessageSettings { Message = "${trigger.title}" }, default));
    }

    [Fact]
    public async Task Execute_28KbLinkUrlOnSiteHost_ButtonDropped_FixedWarning_NeverFailsValidation()
    {
        var rig = new SenderRig();
        var link = "https://www.example.com/" + new string('a', 28_500);
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m", LinkUrl = link }));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        var body = Assert.Single(rig.Handler.Requests).Body;
        Assert.DoesNotContain("Action.OpenUrl", body);
        Assert.DoesNotContain("aaaaaaaaaa", body);
        var warnings = ((TeamsPostOutput)result.OutputData!).Warnings;
        Assert.Contains("The link button was removed: a link longer than 2,048 characters.", warnings);
        Assert.DoesNotContain(warnings, w => w.Contains("aaaa", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_UrlPastedAsCredentialName_FixedMessage_NoRequest_NoEcho()
    {
        var rig = new SenderRig();
        var name = TestUrls.Sentinel;
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }, credential: name));
        Assert.Equal(StepRunErrorCategory.ConfigurationError, result.ErrorCategory);
        Assert.Equal("This looks like a webhook URL. It may have been saved on the connection: clear the field and rotate the Workflow URL (see SECURITY.md#rotation).", result.Exception!.Message);
        Assert.Null(result.Exception.InnerException);
        Assert.Empty(rig.Handler.Requests);
        Assert.All(rig.Logs.AllText.Append(result.Exception.ToString()), t => Assert.False(TestUrls.Leaks(t), t));
    }

    [Fact]
    public async Task Execute_MissingCredential_FailsConfigurationError_NoRequest()
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }, credential: "sentinel2"));
        Assert.Equal(ActionResultStatus.Failed, result.Status);
        Assert.Equal(StepRunErrorCategory.ConfigurationError, result.ErrorCategory);
        Assert.Equal("No value is configured at Umbraco:Automate:MicrosoftTeams:Credentials:sentinel2:WebhookUrl.", result.Exception!.Message);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Execute_429_FailsRateLimiting()
    {
        var rig = new SenderRig(handler: new RecordingHandler { Status = HttpStatusCode.TooManyRequests });
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }));
        Assert.Equal(StepRunErrorCategory.RateLimiting, result.ErrorCategory);
    }

    [Fact]
    public async Task Execute_RateLimiterFull_FailsRateLimiting()
    {
        var rig = new SenderRig(configure: o => o.RateLimit.MaxWaitSeconds = 0);
        for (var i = 0; i < 4; i++)
        {
            await rig.Limiter.WaitAsync("sentinel", TimeSpan.Zero, default);
        }

        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }));
        Assert.Equal(StepRunErrorCategory.RateLimiting, result.ErrorCategory);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Execute_404_FailsConfigurationError_WithDeletedMessage()
    {
        var rig = new SenderRig(handler: new RecordingHandler { Status = HttpStatusCode.NotFound });
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }));
        Assert.Equal(StepRunErrorCategory.ConfigurationError, result.ErrorCategory);
        Assert.Equal("The workflow was deleted or turned off.", result.Exception!.Message);
    }

    [Fact]
    public async Task Execute_100KbBoundMessage_SentWithinBudget_WarningNamesField()
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = new string('m', 100_000) }));
        var output = (TeamsPostOutput)result.OutputData!;
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(output.PayloadBytes <= rig.Options.MaxPayloadBytes);
        Assert.Contains("Card value 'Message' was shortened to fit.", output.Warnings);
    }

    [Fact]
    public async Task Execute_EmojiPayloadOverLimit_ShortenedAndSent()
    {
        var rig = new SenderRig(configure: o => o.MaxValueLength = 100_000);
        var message = string.Concat(Enumerable.Repeat("😀", 14_000)); // 28,000 chars, 12 bytes each once escaped
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = message }));
        var output = (TeamsPostOutput)result.OutputData!;
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(output.PayloadBytes <= rig.Options.MaxPayloadBytes);
    }

    public static TheoryData<string> AdversarialData => new() { "100kb", "controls", "stars", "emoji", "manyrows" };

    private static PostMessageSettings Adversarial(string kind) => kind switch
    {
        "100kb" => new PostMessageSettings { Title = new string('t', 100_000), Message = new string('m', 100_000), Data = [new CardDataRow { Key = "notes", Value = new string('n', 100_000) }] },
        "controls" => new PostMessageSettings { Message = "m", Data = [.. Enumerable.Range(0, 50).Select(i => new CardDataRow { Key = $"k{i}", Value = new string('\u0001', 2000) })] },
        "stars" => new PostMessageSettings { Message = "m", Data = [.. Enumerable.Range(0, 30).Select(i => new CardDataRow { Key = $"k{i}", Value = new string('*', 2000) })] },
        "emoji" => new PostMessageSettings { Message = string.Concat(Enumerable.Repeat("👍🏽", 1000)), Data = [new CardDataRow { Key = "e", Value = string.Concat(Enumerable.Repeat("😀", 1500)) }] },
        _ => new PostMessageSettings { Message = "m", Data = [.. Enumerable.Range(0, 200).Select(i => new CardDataRow { Key = $"key{i}", Value = new string('x', 1000) })] },
    };

    [Theory]
    [MemberData(nameof(AdversarialData))]
    public async Task Execute_AdversarialData_NeverFailsValidation(string kind)
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, Adversarial(kind)));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(((TeamsPostOutput)result.OutputData!).PayloadBytes <= rig.Options.MaxPayloadBytes);
    }

    [Fact]
    public async Task Execute_LoneSurrogateInBoundValues_PostsWithReplacementCharacter()
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Title = "t\uD800", Message = "m\uDC00x", Data = [new CardDataRow { Key = "k\uD800", Value = "v\uD800" }] }));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.Contains("m\uFFFDx", AllText(rig));
    }

    [Fact]
    public async Task Execute_RelativeLink_PostsWithoutButton_WarningInOutput()
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m", LinkUrl = "/news/item" }));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.DoesNotContain("Action.OpenUrl", AllText(rig));
        Assert.Contains("The link button was removed: not an absolute https link.", ((TeamsPostOutput)result.OutputData!).Warnings);
    }

    [Fact]
    public async Task Execute_PersonalDataDataKey_WarningInOutput()
    {
        var rig = new SenderRig();
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m", Data = [new CardDataRow { Key = "email", Value = "a@b.example" }] }));
        Assert.Contains("Card key 'email' may hold personal data. See the GDPR notes.", ((TeamsPostOutput)result.OutputData!).Warnings);
    }

    [Fact]
    public async Task Execute_Success_OutputHasStatusAcceptedBytes()
    {
        var rig = new SenderRig();
        var output = (TeamsPostOutput)(await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }))).OutputData!;
        Assert.True(output.Accepted);
        Assert.Equal(202, output.StatusCode);
        Assert.True(output.PayloadBytes > 0);
    }

    [Fact]
    public async Task Execute_UnknownHost_OutputWarningHasNoHost()
    {
        var rig = new SenderRig("https://SENTINELSIG.example.org/p");
        var output = (TeamsPostOutput)(await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }))).OutputData!;
        Assert.Contains("The webhook host isn't a known Power Automate host.", output.Warnings);
        Assert.DoesNotContain(output.Warnings, w => w.Contains("SENTINEL", StringComparison.OrdinalIgnoreCase));
    }

    public static TheoryData<string> EveryFailureOutcome => new()
    {
        "missing", "badname", "guard", "blocked", "400-echo", "401", "404", "429", "500", "timeout", "http-exception", "limiter-full",
    };

    [Theory]
    [MemberData(nameof(EveryFailureOutcome))]
    public async Task Execute_EveryFailure_ExceptionAndLogsFreeOfSentinel_NullInner(string outcome)
    {
        var handler = outcome switch
        {
            "400-echo" => new RecordingHandler { Status = HttpStatusCode.BadRequest, Body = $"bad {TestUrls.Sentinel}" },
            "401" => new RecordingHandler { Status = HttpStatusCode.Unauthorized, Body = TestUrls.Sentinel },
            "404" => new RecordingHandler { Status = HttpStatusCode.NotFound, Body = TestUrls.Sentinel },
            "429" => new RecordingHandler { Status = HttpStatusCode.TooManyRequests },
            "500" => new RecordingHandler { Status = HttpStatusCode.InternalServerError, Body = TestUrls.Sentinel },
            "timeout" => new RecordingHandler { Hang = true },
            "http-exception" => new RecordingHandler { Throw = new HttpRequestException($"failed {TestUrls.Sentinel}", new InvalidOperationException(TestUrls.Sentinel)) },
            _ => new RecordingHandler(),
        };
        var rig = new SenderRig(
            url: outcome == "blocked" ? "http://x.example/p?sig=SENTINELSIG" : TestUrls.Sentinel,
            configure: o => { o.TimeoutSeconds = 1; if (outcome == "limiter-full") { o.RateLimit.MaxWaitSeconds = 0; } },
            extraAllowedPrefixes: outcome == "guard" ? ["Umbraco:Automate"] : null,
            handler: handler);
        if (outcome == "limiter-full")
        {
            for (var i = 0; i < 4; i++)
            {
                await rig.Limiter.WaitAsync("sentinel", TimeSpan.Zero, default);
            }
        }

        var credential = outcome switch { "missing" => "sentinel2", "badname" => "ops-alerts", _ => "sentinel" };
        var result = await Run(rig, Harness(rig, new PostMessageSettings { Message = "m" }, credential));

        Assert.Equal(ActionResultStatus.Failed, result.Status);
        Assert.IsType<TeamsPostException>(result.Exception);
        Assert.Null(result.Exception!.InnerException);
        Assert.False(TestUrls.Leaks(result.Exception.ToString()));
        Assert.All(rig.Logs.AllText, t => Assert.False(TestUrls.Leaks(t), t));
    }
}
