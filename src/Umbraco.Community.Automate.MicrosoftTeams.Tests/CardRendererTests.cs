using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class CardRendererTests
{
    private static readonly CardRenderer Renderer = new();

    private static LinkChecker Links(params string[] allowed) => new([("www.example.com", false)], allowed);

    private static CardRenderOptions Options(bool escape = false, string version = "1.4", string? summary = null, bool external = false) => new(escape, version, summary, external);

    private static Dictionary<string, string> Data(params (string Key, string Value)[] rows) => rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);

    private static JsonObject Card(params JsonNode[] body) => new() { ["type"] = "AdaptiveCard", ["version"] = "1.4", ["body"] = new JsonArray(body) };

    private static JsonObject Text(string text) => new() { ["type"] = "TextBlock", ["text"] = text };

    private static JsonObject OpenUrl(string url) => new() { ["type"] = "Action.OpenUrl", ["title"] = "Open", ["url"] = url };

    private static JsonObject Render(JsonNode template, Dictionary<string, string> data, CardRenderOptions options, out IReadOnlyList<string> warnings, LinkChecker? links = null)
    {
        var result = Renderer.Render(template, data, options, links ?? Links());
        warnings = result.Warnings;
        return result.Envelope;
    }

    private static JsonObject Content(JsonObject envelope, int index = 0) => (JsonObject)envelope["attachments"]![index]!["content"]!;

    private static string FirstText(JsonObject envelope) => Content(envelope)["body"]![0]!["text"]!.GetValue<string>();

    [Fact]
    public void Render_ReplacesPlaceholdersInStringValuesOnly()
    {
        var template = Card(new JsonObject { ["type"] = "TextBlock", ["text"] = "{{title}}", ["{{title}}"] = "x", ["maxLines"] = 3, ["wrap"] = true });
        var content = Content(Render(template, Data(("title", "Hi")), Options(), out _));
        var block = content["body"]![0]!.AsObject();
        Assert.Equal("Hi", block["text"]!.GetValue<string>());
        Assert.Equal("x", block["{{title}}"]!.GetValue<string>()); // a property name is never a placeholder, and its value has none
        Assert.Equal(3, block["maxLines"]!.GetValue<int>());
        Assert.True(block["wrap"]!.GetValue<bool>());
    }

    [Fact]
    public void Render_MixedTextAndSeveralPlaceholders()
    {
        var envelope = Render(Card(Text("Now live: {{title}} by {{ editor }}")), Data(("title", "Home"), ("editor", "Sam")), Options(), out _);
        Assert.Equal("Now live: Home by Sam", FirstText(envelope));
    }

    [Fact]
    public void Render_KeyMatchIsCaseInsensitive()
    {
        var envelope = Render(Card(Text("{{TITLE}}")), Data(("Title", "Home")), Options(), out _);
        Assert.Equal("Home", FirstText(envelope));
    }

    [Fact]
    public void Render_UnknownKey_EmptyAndWarning()
    {
        var envelope = Render(Card(Text("a{{missing}}b")), Data(), Options(), out var warnings);
        Assert.Equal("ab", FirstText(envelope));
        Assert.Contains("Unknown card key: missing", warnings);
    }

    public static TheoryData<string> AwkwardStrings => new()
    {
        "quote \" and 'single'", "brace } and { and {{", "back\\slash \\\\ \\n", "tab\tand\r\nnewline", "line\u2028sep\u2029para",
        "nul\0char", "price $1 and ${x} and $&", "emoji 😀👍🏽", "(?<x>.*)", "</script><b>",
    };

    [Theory]
    [MemberData(nameof(AwkwardStrings))]
    public void Render_AwkwardStrings_ValidJson_ValueIntact(string value)
    {
        var envelope = Render(Card(Text("{{v}}")), Data(("v", value)), Options(escape: false), out _);
        var json = CardEnvelope.Serialize(envelope).Json;
        using var parsed = JsonDocument.Parse(json); // throws on invalid JSON
        var text = parsed.RootElement.GetProperty("attachments")[0].GetProperty("content").GetProperty("body")[0].GetProperty("text").GetString();
        Assert.Equal(value, text);
    }

    [Fact]
    public void Render_LoneSurrogate_BecomesReplacementCharacter_AndStillSerialises()
    {
        var envelope = Render(Card(Text("{{v}}")), Data(("v", "a\uD800b\uDC00c😀")), Options(), out _);
        Assert.Equal("a\uFFFDb\uFFFDc😀", FirstText(envelope));
        using var parsed = JsonDocument.Parse(CardEnvelope.Serialize(envelope).Json);
    }

    [Fact]
    public void Render_EscapeOn_EscapesDataNotTemplateText()
    {
        var envelope = Render(Card(Text("**bold** {{x}}")), Data(("x", "[x](y)")), Options(escape: true), out _);
        Assert.Equal("**bold** \\[x\\]\\(y\\)", FirstText(envelope));
    }

    [Fact]
    public void Render_EscapeOn_BreaksBareLinksInData()
    {
        var envelope = Render(Card(Text("see https://own.example {{x}}")), Data(("x", "https://a.example www.b.example me@c.example")), Options(escape: true), out _);
        var text = FirstText(envelope);
        Assert.StartsWith("see https://own.example ", text); // the template's own link is untouched
        Assert.Contains("https:\u200B//a.example", text);
        Assert.Contains("www.\u200Bb.example", text);
        Assert.Contains("me\u200B@c.example", text);
    }

    public static TheoryData<string> UrlNames => new() { "url", "URL", "iconUrl", "ICONURL", "imageUrl", "poster", "Poster", "backgroundImage", "BACKGROUNDIMAGE" };

    [Theory]
    [MemberData(nameof(UrlNames))]
    public void Render_UrlProperties_NotEscaped(string name)
    {
        // An element with a custom type, so nothing else about it is special.
        var node = new JsonObject { ["type"] = "Custom", [name] = "{{u}}" };
        var envelope = Render(Card(node), Data(("u", "https://www.example.com/a_b-c*d")), Options(escape: true), out var warnings);
        Assert.Equal("https://www.example.com/a_b-c*d", Content(envelope)["body"]![0]![name]!.GetValue<string>());
        Assert.Empty(warnings);
    }

    [Theory]
    [InlineData("/relative")]
    [InlineData("javascript:x")]
    [InlineData("")]
    [InlineData("https://evil.example/x")]
    [InlineData("http://evil.example/x")]
    public void Render_BadOrExternalUrlInOpenUrl_RemovesAction_AddsWarning(string value)
    {
        var template = Card(Text("t"));
        template["actions"] = new JsonArray(OpenUrl("{{u}}"), OpenUrl("https://www.example.com/keep"));
        var envelope = Render(template, Data(("u", value)), Options(), out var warnings);
        var actions = Content(envelope)["actions"]!.AsArray();
        Assert.Single(actions);
        Assert.Equal("https://www.example.com/keep", actions[0]!["url"]!.GetValue<string>());
        Assert.Single(warnings, w => w.StartsWith("The 'Action.OpenUrl' link was removed: ", StringComparison.Ordinal));
        Assert.DoesNotContain(warnings, w => w.Contains("evil", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_LastActionRemoved_ActionsAndEmptyActionSetGoToo()
    {
        var template = Card(Text("t"), new JsonObject { ["type"] = "ActionSet", ["actions"] = new JsonArray(OpenUrl("https://evil.example")) });
        template["actions"] = new JsonArray(OpenUrl("https://evil.example"));
        var content = Content(Render(template, Data(), Options(), out _));
        Assert.Single(content["body"]!.AsArray());
        Assert.False(content.ContainsKey("actions"));
    }

    [Fact]
    public void Render_ExternalUrl_AllowExternalLinks_Kept()
    {
        var template = Card(Text("t"));
        template["actions"] = new JsonArray(OpenUrl("https://evil.example/x"));
        var envelope = Render(template, Data(), Options(external: true), out var warnings);
        Assert.Equal("https://evil.example/x", Content(envelope)["actions"]![0]!["url"]!.GetValue<string>());
        Assert.Empty(warnings);
    }

    [Fact]
    public void Render_AllowedLinkHost_Kept()
    {
        var template = Card(Text("t"));
        template["actions"] = new JsonArray(OpenUrl("https://docs.partner.example/x"));
        var envelope = Render(template, Data(), Options(), out _, Links("*.partner.example"));
        Assert.Single(Content(envelope)["actions"]!.AsArray());
    }

    [Fact]
    public void Render_BadImageUrl_RemovesElement_AddsWarning()
    {
        var template = Card(Text("keep"), new JsonObject { ["type"] = "Image", ["url"] = "{{img}}" });
        var content = Content(Render(template, Data(("img", "javascript:alert(1)")), Options(), out var warnings));
        Assert.Single(content["body"]!.AsArray());
        Assert.Contains("The 'Image' link was removed: not an absolute https link.", warnings);
    }

    [Fact]
    public void Render_BackgroundImageAndPosterPlaceholders_UrlChecked_NotEscaped()
    {
        var card = Card(
            new JsonObject { ["type"] = "Media", ["poster"] = "{{p}}", ["sources"] = new JsonArray(new JsonObject { ["mimeType"] = "video/mp4", ["url"] = "{{s}}" }) });
        card["backgroundImage"] = "{{bg}}";
        var good = Render(card, Data(("p", "https://www.example.com/p_1.png"), ("s", "https://www.example.com/v_1.mp4"), ("bg", "https://www.example.com/b_1.png")), Options(escape: true), out var none);
        Assert.Empty(none);
        Assert.Equal("https://www.example.com/b_1.png", Content(good)["backgroundImage"]!.GetValue<string>());
        Assert.Equal("https://www.example.com/p_1.png", Content(good)["body"]![0]!["poster"]!.GetValue<string>());
        Assert.Equal("https://www.example.com/v_1.mp4", Content(good)["body"]![0]!["sources"]![0]!["url"]!.GetValue<string>());

        var bad = Content(Render(card, Data(("p", "http://evil.example/p"), ("s", "https://evil.example/v"), ("bg", "/x")), Options(), out var warnings));
        Assert.False(bad.ContainsKey("backgroundImage"));
        Assert.Empty(bad["body"]!.AsArray()); // every source was bad, so the Media element went too
        Assert.Equal(3, warnings.Count(w => w.Contains("link was removed", StringComparison.Ordinal)));
    }

    [Fact]
    public void Render_BackgroundImageObjectWithBadUrl_RemovesTheProperty()
    {
        var card = Card(Text("t"));
        card["backgroundImage"] = new JsonObject { ["url"] = "https://evil.example/x", ["fillMode"] = "Cover" };
        var content = Content(Render(card, Data(), Options(), out _));
        Assert.False(content.ContainsKey("backgroundImage"));
    }

    [Fact]
    public void Render_SelectActionWithBadUrl_RemovesTheSelectAction()
    {
        var container = new JsonObject { ["type"] = "Container", ["selectAction"] = OpenUrl("https://evil.example/x"), ["items"] = new JsonArray(Text("t")) };
        var content = Content(Render(Card(container), Data(), Options(), out _));
        var kept = content["body"]![0]!.AsObject();
        Assert.False(kept.ContainsKey("selectAction"));
        Assert.Single(kept["items"]!.AsArray());
    }

    [Fact]
    public void Render_EnvelopeWithTwoAttachments_RulesApplyToBoth()
    {
        var other = new JsonObject { ["contentType"] = "application/vnd.microsoft.card.thumbnail", ["contentUrl"] = "https://evil.example/x", ["content"] = new JsonObject { ["title"] = "{{t}}" } };
        var adaptive = new JsonObject { ["contentType"] = "application/vnd.microsoft.card.adaptive", ["content"] = Card(Text("{{t}}")) };
        var envelope = new JsonObject { ["type"] = "message", ["attachments"] = new JsonArray(adaptive, other) };
        var result = Render(envelope, Data(("t", "a_b")), Options(escape: true), out var warnings);
        Assert.Equal("a\\_b", FirstText(result));
        Assert.Equal("a\\_b", Content(result, 1)["title"]!.GetValue<string>());
        Assert.False(result["attachments"]![1]!.AsObject().ContainsKey("contentUrl"));
        Assert.Contains("The 'contentUrl' link was removed: links to a host outside AllowedLinkHosts.", warnings);
    }

    [Fact]
    public void Render_DataContainingPlaceholder_IsNotReSubstituted()
    {
        var envelope = Render(Card(Text("{{title}}")), Data(("title", "{{secret}}"), ("secret", "x")), Options(), out var warnings);
        Assert.Equal("{{secret}}", FirstText(envelope));
        Assert.DoesNotContain(warnings, w => w.StartsWith("Unknown card key", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_KeysWithRegexSpecialCharacters_Literal()
    {
        var envelope = Render(Card(Text("{{a.b}}|{{a-b}}")), Data(("a.b", "$1 \\ (?"), ("a-b", "$& ${0}")), Options(), out var warnings);
        Assert.Equal("$1 \\ (?|$& ${0}", FirstText(envelope));
        Assert.Empty(warnings);
    }

    [Fact]
    public void PlaceholderRegex_HasMatchTimeout() => Assert.Equal(TimeSpan.FromSeconds(1), CardRenderer.PlaceholderPattern.MatchTimeout);

    [Fact]
    public void Render_PersonalDataKey_WarningNamesKey()
    {
        Render(Card(Text("t")), Data(("email", "a@b.example"), ("title", "x")), Options(), out var warnings);
        Assert.Contains("Card key 'email' may hold personal data. See the GDPR notes.", warnings);
        Assert.DoesNotContain(warnings, w => w.Contains("'title'", StringComparison.Ordinal));
    }

    [Fact]
    public void Render_KeepsAuthorVersion_SetsDefaultOnlyWhenMissing()
    {
        var authored = Card(Text("t"));
        authored["version"] = "1.2";
        Assert.Equal("1.2", Content(Render(authored, Data(), Options(version: "1.5"), out _))["version"]!.GetValue<string>());

        var missing = Card(Text("t"));
        missing.Remove("version");
        Assert.Equal("1.5", Content(Render(missing, Data(), Options(version: "1.5"), out _))["version"]!.GetValue<string>());
    }

    [Fact]
    public void Render_FullEnvelope_NotDoubleWrapped()
    {
        var envelope = new JsonObject { ["type"] = "message", ["attachments"] = new JsonArray(new JsonObject { ["contentType"] = "application/vnd.microsoft.card.adaptive", ["content"] = Card(Text("t")) }) };
        var result = Render(envelope, Data(), Options(), out _);
        Assert.Equal("message", result["type"]!.GetValue<string>());
        Assert.Single(result["attachments"]!.AsArray());
        Assert.Equal("AdaptiveCard", Content(result)["type"]!.GetValue<string>());
    }

    [Fact]
    public void Render_BareCard_IsWrappedOnce()
    {
        var result = Render(Card(Text("t")), Data(), Options(), out _);
        Assert.Equal("message", result["type"]!.GetValue<string>());
        Assert.Equal("application/vnd.microsoft.card.adaptive", result["attachments"]![0]!["contentType"]!.GetValue<string>());
    }

    [Fact]
    public void Render_DoesNotMutateTemplate()
    {
        var template = Card(Text("{{x}}"));
        template["actions"] = new JsonArray(OpenUrl("https://evil.example"));
        var before = template.ToJsonString();
        Render(template, Data(("x", "y")), Options(escape: true, summary: "s"), out _);
        Assert.Equal(before, template.ToJsonString());
    }

    [Fact]
    public void Render_AppliesSummary()
    {
        var result = Render(Card(Text("t")), Data(), Options(summary: "Sum"), out _);
        Assert.Equal("Sum", result["summary"]!.GetValue<string>());
        Assert.Equal("Sum", Content(result)["fallbackText"]!.GetValue<string>());
    }

    [Fact]
    public void Render_NotAnObject_Throws() =>
        Assert.Throws<ArgumentException>(() => Renderer.Render(new JsonArray(), Data(), Options(), Links()));

    [Fact]
    public void Render_UnknownRootType_Throws() =>
        Assert.Throws<ArgumentException>(() => Renderer.Render(new JsonObject { ["type"] = "Other" }, Data(), Options(), Links()));
}
