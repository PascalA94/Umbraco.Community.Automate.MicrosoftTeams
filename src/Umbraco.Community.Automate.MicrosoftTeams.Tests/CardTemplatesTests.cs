using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Json.Schema;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

// Contract/adaptive-card-1.4.schema.json is the Adaptive Cards 1.4 schema, fetched 2026-11-16 from
// https://raw.githubusercontent.com/microsoft/AdaptiveCards/main/schemas/1.4.0/adaptive-card.json
// (adaptivecards.io serves only the unversioned latest schema, so the repo's 1.4.0 copy is the pinned source).
// The rendered snapshots in Contract/Snapshots are what a desktop client should be shown; the Live gate (S5) re-checks them.
public partial class CardTemplatesTests
{
    private static readonly CardTemplates Templates = new();
    private static readonly CardRenderer Renderer = new();

    public static TheoryData<CardSource> BuiltIns => new() { CardSource.ContentPublished, CardSource.ApprovalNeeded, CardSource.FormSubmitted, CardSource.Simple };

    public static TheoryData<CardSource> BuiltInsWithUrl => BuiltIns;

    private static readonly Dictionary<CardSource, string> SnapshotNames = new()
    {
        [CardSource.ContentPublished] = "content-published",
        [CardSource.ApprovalNeeded] = "approval-needed",
        [CardSource.FormSubmitted] = "form-submitted",
        [CardSource.Simple] = "simple",
    };

    private static Dictionary<string, string> Sample(CardSource source) => Templates.AllKeys(source).ToDictionary(
        k => k,
        k => k switch
        {
            "url" => "https://www.example.com/news/launch",
            "title" => "Launch day_1",
            "text" => "The *new* site is live.",
            "linkLabel" => "Read the post",
            "formName" => "Contact us",
            "entryId" => "1042",
            _ => k + " value",
        },
        StringComparer.OrdinalIgnoreCase);

    private static LinkChecker Links() => new([("www.example.com", false)], []);

    private static JsonObject RenderSample(CardSource source, Dictionary<string, string>? data = null, bool escape = true) =>
        Renderer.Render(Templates.Get(source), data ?? Sample(source), new CardRenderOptions(escape, "1.4", null, false), Links()).Envelope;

    private static JsonNode Resource(string name)
    {
        using var stream = typeof(CardTemplatesTests).Assembly.GetManifestResourceStream(name)!;
        return JsonNode.Parse(stream)!;
    }

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void Template_RendersToSnapshot(CardSource source)
    {
        var expected = Resource($"Snapshots.{SnapshotNames[source]}.json");
        var actual = RenderSample(source);
        Assert.True(JsonNode.DeepEquals(expected, actual), actual.ToJsonString(new() { WriteIndented = true }));
    }

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void Template_ValidatesAgainstAdaptiveCard14Schema(CardSource source)
    {
        var schema = JsonSchema.FromText(Resource("adaptive-card-1.4.schema.json").ToJsonString());
        var content = RenderSample(source)["attachments"]![0]!["content"]!;
        var result = schema.Evaluate(content, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(result.IsValid, string.Join('\n', result.Details.Where(d => d.Errors is not null).SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Key} {e.Value}"))));
    }

    [Fact]
    public void Schema_RejectsAnInvalidCard_SoTheValidationTestMeansSomething()
    {
        var schema = JsonSchema.FromText(Resource("adaptive-card-1.4.schema.json").ToJsonString());
        var bad = JsonNode.Parse("""{"type":"AdaptiveCard","version":"1.4","body":[{"type":"TextBlock"}]}""")!; // TextBlock needs text
        Assert.False(schema.Evaluate(bad, new EvaluationOptions { OutputFormat = OutputFormat.List }).IsValid);
    }

    private static readonly string[] AllowedTypes = ["AdaptiveCard", "TextBlock", "FactSet", "ColumnSet", "Column", "Action.OpenUrl"];

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void Template_UsesOnlyAllowedElements(CardSource source)
    {
        var types = new List<string>();
        Types(Templates.Get(source), types);
        Assert.All(types, t => Assert.Contains(t, AllowedTypes)); // a Fact has no type of its own
    }

    private static void Types(JsonNode? node, List<string> types)
    {
        switch (node)
        {
            case JsonObject obj:
                if ((obj["type"] as JsonValue)?.GetValue<string>() is { } type)
                {
                    types.Add(type);
                }

                foreach (var (_, child) in obj)
                {
                    Types(child, types);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Types(child, types);
                }

                break;
        }
    }

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void Template_ContainsOnlyItsDeclaredKeys(CardSource source)
    {
        var used = Placeholders().Matches(Templates.Get(source).ToJsonString()).Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(Templates.AllKeys(source).Order(StringComparer.Ordinal), used);
        Assert.All(Templates.RequiredKeys(source), k => Assert.Contains(k, Templates.AllKeys(source)));
    }

    [Theory]
    [InlineData(CardSource.ContentPublished, "title,url", "Content published")]
    [InlineData(CardSource.ApprovalNeeded, "title,url", "Approval needed")]
    [InlineData(CardSource.FormSubmitted, "formName,url", "Form submitted")]
    [InlineData(CardSource.Simple, "title,text", "Simple")]
    [InlineData(CardSource.Custom, "", "Custom")]
    public void RequiredKeysAndDisplayName_MatchTheSpecTable(CardSource source, string required, string display)
    {
        Assert.Equal(required.Length == 0 ? [] : required.Split(','), Templates.RequiredKeys(source));
        Assert.Equal(display, Templates.DisplayName(source));
    }

    [Fact]
    public void FrozenKeyTable_IsExactlyTheSpec()
    {
        Assert.Equal(["title", "url", "contentType", "editor", "publishedAt"], Templates.AllKeys(CardSource.ContentPublished));
        Assert.Equal(["title", "url", "editor", "requestedAt", "notes"], Templates.AllKeys(CardSource.ApprovalNeeded));
        Assert.Equal(["formName", "url", "entryId", "submittedAt"], Templates.AllKeys(CardSource.FormSubmitted));
        Assert.Equal(["title", "text", "url", "linkLabel"], Templates.AllKeys(CardSource.Simple));
    }

    [Fact]
    public void FormSubmitted_ExtraEmailRow_NeverInOutput()
    {
        var data = Sample(CardSource.FormSubmitted);
        data["email"] = "a@b.example";
        Assert.DoesNotContain("a@b.example", RenderSample(CardSource.FormSubmitted, data).ToJsonString());
    }

    [Fact]
    public void Get_ReturnsIndependentClones()
    {
        var first = (JsonObject)Templates.Get(CardSource.Simple);
        first["version"] = "9.9";
        first["body"]!.AsArray().Clear();
        var second = (JsonObject)Templates.Get(CardSource.Simple);
        Assert.Equal("1.4", second["version"]!.GetValue<string>());
        Assert.NotEmpty(second["body"]!.AsArray());
        Assert.NotSame(first, second);
    }

    [Fact]
    public void Get_Custom_Throws() => Assert.Throws<ArgumentException>(() => Templates.Get(CardSource.Custom));

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void NoTemplate_UsesClickHereLabel(CardSource source) =>
        Assert.DoesNotContain("click here", Templates.Get(source).ToJsonString(), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Simple_WithoutUrl_HasNoButton()
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["title"] = "t", ["text"] = "x" };
        var content = RenderSample(CardSource.Simple, data)["attachments"]![0]!["content"]!.AsObject();
        Assert.False(content.ContainsKey("actions"));
    }

    [Theory]
    [MemberData(nameof(BuiltInsWithUrl))]
    public void BuiltIn_ExternalUrl_ButtonDropped_EvenIfAllowExternalLinks(CardSource source)
    {
        var data = Sample(source);
        data["url"] = "https://evil.example/login";
        var allow = CardTemplates.EffectiveAllowExternalLinks(source, requested: true);
        var result = Renderer.Render(Templates.Get(source), data, new CardRenderOptions(true, "1.4", null, allow), Links());
        Assert.False(result.Envelope["attachments"]![0]!["content"]!.AsObject().ContainsKey("actions"));
        Assert.Contains(result.Warnings, w => w.Contains("link was removed", StringComparison.Ordinal));
    }

    [Fact]
    public void EffectiveAllowExternalLinks_OnlyForCustom()
    {
        Assert.True(CardTemplates.EffectiveAllowExternalLinks(CardSource.Custom, true));
        Assert.False(CardTemplates.EffectiveAllowExternalLinks(CardSource.Custom, false));
        Assert.False(CardTemplates.EffectiveAllowExternalLinks(CardSource.Simple, true));
    }

    [Theory]
    [MemberData(nameof(BuiltIns))]
    public void BuiltIn_KeysNeverTriggerPersonalDataWarning(CardSource source) =>
        Assert.Empty(PersonalDataKeys.Warnings(Templates.AllKeys(source)));

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}")]
    private static partial Regex Placeholders();
}
