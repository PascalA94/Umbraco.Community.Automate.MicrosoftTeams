using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

// Contract/adaptive-card-1.5.schema.json is the Adaptive Cards 1.5 schema, fetched 2026-11-16 from
// https://raw.githubusercontent.com/microsoft/AdaptiveCards/main/schemas/1.5.0/adaptive-card.json
// (adaptivecards.io serves only the unversioned latest schema, so the repo's 1.5.0 copy is the pinned source).
public class UrlPropertiesTests
{
    // `$schema` is document metadata that no client fetches, so it is not a link a card can send anyone to.
    private static readonly string[] Metadata = ["$schema"];

    [Fact]
    public void EverySchemaUriProperty_IsOnTheList()
    {
        using var stream = typeof(UrlPropertiesTests).Assembly.GetManifestResourceStream("adaptive-card-1.5.schema.json")!;
        var schema = JsonNode.Parse(stream)!;

        var found = new SortedSet<string>(StringComparer.Ordinal);
        Collect(schema, found);

        Assert.NotEmpty(found); // the walk really reached the properties
        var parent = new JsonObject();
        foreach (var name in found.Except(Metadata))
        {
            Assert.True(UrlProperties.IsUrlProperty(name, parent), name);
            Assert.True(UrlProperties.IsUrlProperty(name.ToUpperInvariant(), parent), name + " upper case");
        }
    }

    // A property is a URL when its own schema, or any alternative of it, has format uri or uri-reference.
    private static void Collect(JsonNode? node, ISet<string> found)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj["properties"] is JsonObject properties)
                {
                    foreach (var (name, definition) in properties)
                    {
                        if (IsUri(definition))
                        {
                            found.Add(name);
                        }
                    }
                }

                foreach (var (_, child) in obj)
                {
                    Collect(child, found);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Collect(child, found);
                }

                break;
        }
    }

    private static bool IsUri(JsonNode? definition) =>
        definition is JsonObject d
        && (((d["format"] as JsonValue)?.GetValue<string>() is "uri" or "uri-reference")
            || (d["anyOf"] is JsonArray alternatives && alternatives.Any(IsUri)));

    [Theory]
    [InlineData("url")]
    [InlineData("URL")]
    [InlineData("iconUrl")]
    [InlineData("imageurl")]
    [InlineData("backgroundImage")]
    [InlineData("poster")]
    public void ListedNames_AreUrlProperties(string name) => Assert.True(UrlProperties.IsUrlProperty(name, new JsonObject()));

    [Theory]
    [InlineData("text")]
    [InlineData("title")]
    [InlineData("type")]
    [InlineData("urlish")]
    public void OtherNames_AreNot(string name) => Assert.False(UrlProperties.IsUrlProperty(name, new JsonObject()));

    [Fact]
    public void ParentThatIsNotAnObject_IsNeverAUrlProperty() =>
        Assert.False(UrlProperties.IsUrlProperty("url", new JsonArray()));
}
