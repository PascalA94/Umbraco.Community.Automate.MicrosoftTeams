using System.Net;
using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class PostCardActionTests
{
    private static CardDataRow Row(string key, string? value) => new() { Key = key, Value = value };

    private static string Custom(string textPlaceholder) =>
        """{"type":"AdaptiveCard","version":"1.4","body":[{"type":"TextBlock","text":"TEXT"}]}""".Replace("TEXT", textPlaceholder);

    private static PostCardSettings CustomCard(string json, params CardDataRow[] rows) => new() { CardSource = CardSource.Custom, CustomCardJson = json, CardData = [.. rows] };

    private static Task<ActionResult> Run(SenderRig rig, PostCardSettings settings, string? credential = "sentinel") =>
        CardActionRig.Run(rig, CardActionRig.Harness<PostCardAction, PostCardSettings>(rig, settings, credential));

    private static JsonElement Content(SenderRig rig) => JsonDocument.Parse(Assert.Single(rig.Handler.Requests).Body).RootElement.GetProperty("attachments")[0].GetProperty("content");

    private static string FirstText(SenderRig rig) => Content(rig).GetProperty("body")[0].GetProperty("text").GetString()!;

    private static string[] Warnings(ActionResult result) => ((TeamsPostOutput)result.OutputData!).Warnings;

    [Fact]
    public async Task Execute_PageNameWithQuoteAndBrace_ValidJsonRendered()
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.ContentPublished, CardData = [Row("title", "Say \"hi\" } {"), Row("url", "https://www.example.com/a")] });
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.Equal("Now live: Say \"hi\" } {", FirstText(rig));
    }

    [Fact]
    public async Task Execute_FormValueLink_EscapingOn_Literal()
    {
        var rig = new SenderRig();
        await Run(rig, CustomCard(Custom("{{v}}"), Row("v", "[Click me](https://evil.example)")));
        var text = FirstText(rig);
        Assert.StartsWith("\\[Click me\\]\\(", text);
        Assert.DoesNotContain("](", text);
    }

    [Fact]
    public async Task Execute_FormValueBareUrl_EscapingOn_LinkBroken()
    {
        var rig = new SenderRig();
        await Run(rig, CustomCard(Custom("{{v}}"), Row("v", "https://evil.example/x www.evil.example a@evil.example")));
        var text = FirstText(rig);
        Assert.Contains("https:​//evil.example", text);
        Assert.Contains("www.​evil.example", text);
        Assert.Contains("a​@evil.example", text);
    }

    [Fact]
    public async Task Execute_FormValueLink_EscapingOff_Markdown()
    {
        var rig = new SenderRig();
        var settings = CustomCard(Custom("{{v}}"), Row("v", "[Click me](https://evil.example)"));
        settings.EscapeMarkdownInData = false;
        await Run(rig, settings);
        Assert.Equal("[Click me](https://evil.example)", FirstText(rig));
    }

    [Fact]
    public async Task Execute_CustomMissingKey_RendersEmpty_WarningNamesKey()
    {
        var rig = new SenderRig();
        var result = await Run(rig, CustomCard(Custom("a{{missing}}b")));
        Assert.Equal("ab", FirstText(rig));
        Assert.Contains("Unknown card key: missing", Warnings(result));
    }

    [Fact]
    public async Task Execute_CustomActionSubmit_WarningInOutput()
    {
        var rig = new SenderRig();
        var json = """{"type":"AdaptiveCard","version":"1.4","body":[],"actions":[{"type":"Action.Submit","title":"Go"}]}""";
        var result = await Run(rig, CustomCard(json));
        Assert.Contains("The card has an Action.Submit button, which Teams Workflows doesn't support.", Warnings(result));
    }

    [Fact]
    public async Task Execute_CustomFullEnvelope_SentAsIs()
    {
        var rig = new SenderRig();
        var json = """{"type":"message","attachments":[{"contentType":"application/vnd.microsoft.card.adaptive","content":{"type":"AdaptiveCard","version":"1.4","body":[{"type":"TextBlock","text":"{{v}}"}]}}]}""";
        await Run(rig, CustomCard(json, Row("v", "hello")));
        var root = JsonDocument.Parse(Assert.Single(rig.Handler.Requests).Body).RootElement;
        Assert.Equal(1, root.GetProperty("attachments").GetArrayLength());
        Assert.Equal("hello", FirstText(rig));
    }

    [Fact]
    public async Task Execute_CustomCardEmailKey_PersonalDataWarningInOutput()
    {
        var rig = new SenderRig();
        var result = await Run(rig, CustomCard(Custom("{{email}}"), Row("email", "a@b.example")));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.Contains("Card key 'email' may hold personal data. See the GDPR notes.", Warnings(result));
    }

    [Fact]
    public async Task Execute_FormSubmittedIgnoredEmailRow_NotSent_IgnoredRowWarning_NoPersonalDataWarning()
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings
        {
            CardSource = CardSource.FormSubmitted,
            CardData = [Row("formName", "Contact"), Row("url", "https://www.example.com/e/1"), Row("email", "a@b.example")],
        });
        Assert.DoesNotContain("a@b.example", rig.Handler.Requests.Single().Body);
        var warnings = Warnings(result);
        Assert.Contains("Card key 'email' isn't used by the Form submitted template, so it wasn't sent.", warnings);
        Assert.DoesNotContain(warnings, w => w.Contains("personal data", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_ApprovalNeeded_ExternalUrl_ButtonDropped()
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings
        {
            CardSource = CardSource.ApprovalNeeded,
            AllowExternalLinks = true, // ignored for built-in templates (F04)
            CardData = [Row("title", "Page"), Row("url", "https://evil.example/login")],
        });
        Assert.False(Content(rig).TryGetProperty("actions", out _));
        Assert.Contains(Warnings(result), w => w.Contains("link was removed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_CustomExternalUrl_AllowExternalLinks_Kept()
    {
        var rig = new SenderRig();
        var json = """{"type":"AdaptiveCard","version":"1.4","body":[],"actions":[{"type":"Action.OpenUrl","title":"Go","url":"https://partner.example/x"}]}""";
        var settings = CustomCard(json);
        settings.AllowExternalLinks = true;
        await Run(rig, settings);
        Assert.Equal("https://partner.example/x", Content(rig).GetProperty("actions")[0].GetProperty("url").GetString());
    }

    [Fact]
    public async Task Execute_OptionalKeysMissing_NoUnknownKeyWarnings()
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.ContentPublished, CardData = [Row("title", "T"), Row("url", "https://www.example.com/a")] });
        Assert.DoesNotContain(Warnings(result), w => w.StartsWith("Unknown card key", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_SimpleWithoutUrl_NoButtonAndNoLinkWarning()
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title", "T"), Row("text", "x")] });
        Assert.False(Content(rig).TryGetProperty("actions", out _));
        Assert.Empty(Warnings(result));
    }

    [Fact]
    public async Task Execute_SimpleWithUrlButNoLabel_ButtonSaysOpen()
    {
        var rig = new SenderRig();
        await Run(rig, new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title", "T"), Row("text", "x"), Row("url", "https://www.example.com/a")] });
        Assert.Equal("Open", Content(rig).GetProperty("actions")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Execute_Summary_SetOnEnvelopeAndCard()
    {
        var rig = new SenderRig();
        await Run(rig, new PostCardSettings { CardSource = CardSource.Simple, Summary = "New post", CardData = [Row("title", "T"), Row("text", "x")] });
        Assert.Equal("New post", JsonDocument.Parse(rig.Handler.Requests.Single().Body).RootElement.GetProperty("summary").GetString());
        Assert.Equal("New post", Content(rig).GetProperty("fallbackText").GetString());
    }

    [Fact]
    public async Task Execute_LoneSurrogateInSummaryAndData_StillPosts()
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.Simple, Summary = "s\uD800" + new string('s', 5000), CardData = [Row("title", "T\uDC00"), Row("text\uD800", "x")] });
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.Contains("T\uFFFD", rig.Handler.Requests.Single().Body);
    }

    [Fact]
    public async Task Execute_100KbCardDataValue_SentWithinBudget_WarningNamesKey()
    {
        var rig = new SenderRig();
        var result = await Run(rig, CustomCard(Custom("{{notes}}"), Row("notes", new string('n', 100_000))));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(((TeamsPostOutput)result.OutputData!).PayloadBytes <= rig.Options.MaxPayloadBytes);
        Assert.Contains("Card value 'notes' was shortened to fit.", Warnings(result));
    }

    private static string ManyKeysCard(int count)
    {
        var blocks = Enumerable.Range(0, count).Select(i => "{\"type\":\"TextBlock\",\"text\":\"{{k" + i + "}}\"}");
        return "{\"type\":\"AdaptiveCard\",\"version\":\"1.4\",\"body\":[" + string.Join(',', blocks) + "]}";
    }

    [Fact]
    public async Task Execute_TwentyKeysOf2000_SentWithinBudget()
    {
        var rig = new SenderRig();
        var rows = Enumerable.Range(0, 20).Select(i => Row($"k{i}", new string('x', 2000))).ToArray();
        var result = await Run(rig, CustomCard(ManyKeysCard(20), rows));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(((TeamsPostOutput)result.OutputData!).PayloadBytes <= rig.Options.MaxPayloadBytes);
        Assert.Contains(Warnings(result), w => w.StartsWith("Card value 'k", StringComparison.Ordinal));
    }

    public static TheoryData<string> AdversarialData => new() { "100kb", "controls", "stars", "emoji", "manyrows" };

    private static PostCardSettings Adversarial(string kind) => kind switch
    {
        "100kb" => CustomCard(ManyKeysCard(3), Row("k0", new string('t', 100_000)), Row("k1", new string('m', 100_000)), Row("k2", new string('n', 100_000))),
        "controls" => CustomCard(ManyKeysCard(50), [.. Enumerable.Range(0, 50).Select(i => Row($"k{i}", new string('\u0001', 2000)))]),
        "stars" => CustomCard(ManyKeysCard(30), [.. Enumerable.Range(0, 30).Select(i => Row($"k{i}", new string('*', 2000)))]),
        "emoji" => CustomCard(ManyKeysCard(2), Row("k0", string.Concat(Enumerable.Repeat("👍🏽", 1000))), Row("k1", string.Concat(Enumerable.Repeat("😀", 1500)))),
        _ => CustomCard(ManyKeysCard(200), [.. Enumerable.Range(0, 200).Select(i => Row($"k{i}", new string('x', 1000)))]),
    };

    [Theory]
    [MemberData(nameof(AdversarialData))]
    public async Task Execute_AdversarialData_NeverFailsValidation(string kind)
    {
        var rig = new SenderRig();
        var result = await Run(rig, Adversarial(kind));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(((TeamsPostOutput)result.OutputData!).PayloadBytes <= rig.Options.MaxPayloadBytes);
    }

    private static string TwoAttachmentCard(string text) =>
        "{\"type\":\"message\",\"attachments\":["
        + string.Join(',', Enumerable.Range(0, 2).Select(_ => "{\"contentType\":\"application/vnd.microsoft.card.adaptive\",\"content\":{\"type\":\"AdaptiveCard\",\"version\":\"1.4\",\"body\":[{\"type\":\"TextBlock\",\"text\":\"" + text + "\"}]}}"))
        + "]}";

    public static TheoryData<string, string> AdversarialSummaryCases
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var kind in new[] { "controls", "emoji" })
            {
                foreach (var target in new[] { "custom", "twoAttachments", "ContentPublished", "ApprovalNeeded", "FormSubmitted", "Simple" })
                {
                    data.Add(kind, target);
                }
            }

            return data;
        }
    }

    private static PostCardSettings ForTarget(string target, string summary) => target switch
    {
        "custom" => CustomCard(Custom("{{v}}"), Row("v", "keep me")),
        "twoAttachments" => CustomCard(TwoAttachmentCard("{{v}}"), Row("v", "keep me")),
        "ContentPublished" => new PostCardSettings { CardSource = CardSource.ContentPublished, CardData = [Row("title", "keep me"), Row("url", "https://www.example.com/a")] },
        "ApprovalNeeded" => new PostCardSettings { CardSource = CardSource.ApprovalNeeded, CardData = [Row("title", "keep me"), Row("url", "https://www.example.com/a")] },
        "FormSubmitted" => new PostCardSettings { CardSource = CardSource.FormSubmitted, CardData = [Row("formName", "keep me"), Row("url", "https://www.example.com/a")] },
        _ => new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title", "keep me"), Row("text", "keep me")] },
    };

    [Theory]
    [MemberData(nameof(AdversarialSummaryCases))]
    public async Task Execute_AdversarialSummary_NeverFailsValidation(string kind, string target)
    {
        var rig = new SenderRig();
        var summary = kind == "controls" ? new string('\u0001', 2000) : string.Concat(Enumerable.Repeat("😀", 1000));
        var settings = ForTarget(target, summary);
        settings.Summary = summary;
        var result = await Run(rig, settings);
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.True(((TeamsPostOutput)result.OutputData!).PayloadBytes <= rig.Options.MaxPayloadBytes);

        // Data is never emptied to make room for the summary.
        Assert.DoesNotContain(Warnings(result), w => w.Contains("removed to fit", StringComparison.Ordinal));
        Assert.Contains("keep me", rig.Handler.Requests.Single().Body);
        if (kind == "controls")
        {
            Assert.False(JsonDocument.Parse(rig.Handler.Requests.Single().Body).RootElement.TryGetProperty("summary", out _));
        }
    }

    [Fact]
    public async Task Execute_SummaryOverBudget_SummaryShortenedBeforeData_WarningNamesSummary()
    {
        var rig = new SenderRig(configure: o => o.MaxPayloadBytes = 2500);
        var result = await Run(rig, WithSummary(CustomCard(Custom("{{v}}"), Row("v", new string('d', 100))), string.Concat(Enumerable.Repeat("😀", 125))));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        var warnings = Warnings(result);
        Assert.Contains("Card value 'Summary' was shortened to fit.", warnings);
        Assert.DoesNotContain(warnings, w => w.Contains("(summary)", StringComparison.Ordinal) || w.Contains("'v'", StringComparison.Ordinal));
        Assert.Contains(new string('d', 100), rig.Handler.Requests.Single().Body);
    }

    private static PostCardSettings WithSummary(PostCardSettings s, string summary)
    {
        s.Summary = summary;
        return s;
    }

    [Fact]
    public async Task Execute_Summary_StripsControlAndBidi_CapsAt250()
    {
        var rig = new SenderRig();
        await Run(rig, WithSummary(CustomCard(Custom("x")), "‮ab\u0007c" + new string('s', 400)));
        var summary = JsonDocument.Parse(rig.Handler.Requests.Single().Body).RootElement.GetProperty("summary").GetString()!;
        Assert.StartsWith("abcsss", summary);
        Assert.DoesNotContain('‮', summary);
        Assert.True(summary.Length <= 251);
    }

    [Fact]
    public async Task Execute_Custom4KbTemplate_WithBoundSummary_Fits()
    {
        var rig = new SenderRig();
        var result = await Run(rig, WithSummary(CustomCard(Custom(new string('t', 4000) + "{{v}}"), Row("v", "keep me")), new string('s', 250)));
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.Empty(Warnings(result));
        Assert.Equal(new string('s', 250), JsonDocument.Parse(rig.Handler.Requests.Single().Body).RootElement.GetProperty("summary").GetString());
    }

    [Fact]
    public async Task Execute_CustomTemplateTooBigWhenEmpty_FailsValidation_NoRequest()
    {
        var rig = new SenderRig();
        var result = await Run(rig, CustomCard(Custom(new string('t', 30_000))));
        Assert.Equal(StepRunErrorCategory.Validation, result.ErrorCategory);
        Assert.StartsWith("Card is ", result.Exception!.Message);
        Assert.Empty(rig.Handler.Requests);
    }

    [Theory]
    [InlineData("{ nope")]
    [InlineData("[]")]
    [InlineData("""{"type":"Other"}""")]
    [InlineData(null)]
    public async Task Execute_CustomInvalidJsonAtRunTime_FailsValidation_NoRequest(string? json)
    {
        var rig = new SenderRig();
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = json });
        Assert.Equal(StepRunErrorCategory.Validation, result.ErrorCategory);
        Assert.IsType<TeamsPostException>(result.Exception);
        Assert.Empty(rig.Handler.Requests);
    }

    private static PostCardSettings Simple() => new() { CardSource = CardSource.Simple, CardData = [Row("title", "T"), Row("text", "x")] };

    [Fact]
    public async Task Execute_28KbUrlDataKey_ButtonDropped_NeverFailsValidation()
    {
        var rig = new SenderRig(configure: o => o.MaxValueLength = 50_000);
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title", "T"), Row("text", "x"), Row("url", "https://www.example.com/" + new string('a', 28_500))] });
        Assert.Equal(ActionResultStatus.Success, result.Status);
        Assert.False(Content(rig).TryGetProperty("actions", out _));
        Assert.DoesNotContain("aaaaaaaaaa", rig.Handler.Requests.Single().Body);
        Assert.DoesNotContain(Warnings(result), w => w.Contains("aaaa", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Execute_UrlPastedAsCredentialName_FixedMessage_NoRequest_NoEcho()
    {
        var rig = new SenderRig();
        var name = TestUrls.Sentinel;
        var result = await Run(rig, new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title", "T"), Row("text", "x")] }, name);
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
        var result = await Run(rig, Simple(), credential: "sentinel2");
        Assert.Equal(StepRunErrorCategory.ConfigurationError, result.ErrorCategory);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Execute_RateLimiterFull_FailsRateLimiting()
    {
        var rig = new SenderRig(configure: o => o.RateLimit.MaxWaitSeconds = 0);
        for (var i = 0; i < 4; i++)
        {
            await rig.Limiter.WaitAsync("sentinel", TimeSpan.Zero, default);
        }

        var result = await Run(rig, Simple());
        Assert.Equal(StepRunErrorCategory.RateLimiting, result.ErrorCategory);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task TwoActions_SameCredential_ShareBucket()
    {
        var rig = new SenderRig(configure: o => o.RateLimit.MaxWaitSeconds = 0);
        for (var i = 0; i < 4; i++)
        {
            var sent = await CardActionRig.Run(rig, CardActionRig.Harness<PostMessageAction, PostMessageSettings>(rig, new PostMessageSettings { Message = "m" }));
            Assert.Equal(ActionResultStatus.Success, sent.Status);
        }

        var result = await Run(rig, Simple());
        Assert.Equal(StepRunErrorCategory.RateLimiting, result.ErrorCategory);
        Assert.Equal(4, rig.Handler.Requests.Count); // the card never reached the webhook
    }

    public static TheoryData<string> EveryFailureOutcome => new()
    {
        "missing", "badname", "guard", "blocked", "400-echo", "401", "404", "429", "500", "timeout", "http-exception", "limiter-full",
    };

    [Theory]
    [MemberData(nameof(EveryFailureOutcome))]
    public async Task Execute_EveryFailure_RunLogAndExceptionFreeOfSentinel_NullInner(string outcome)
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
        var result = await Run(rig, Simple(), credential);

        Assert.Equal(ActionResultStatus.Failed, result.Status);
        Assert.IsType<TeamsPostException>(result.Exception);
        Assert.Null(result.Exception!.InnerException);
        Assert.False(TestUrls.Leaks(result.Exception.ToString()));
        Assert.All(rig.Logs.AllText, t => Assert.False(TestUrls.Leaks(t), t));
    }
}
