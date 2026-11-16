using System.Text;
using System.Text.Json.Nodes;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class CardEnvelopeTests
{
    private static JsonObject Card(string text = "hi") => new()
    {
        ["type"] = "AdaptiveCard",
        ["version"] = "1.4",
        ["body"] = new JsonArray(new JsonObject { ["type"] = "TextBlock", ["text"] = text }),
    };

    [Fact]
    public void Wrap_BareCard_IsWrappedOnce()
    {
        var e = CardEnvelope.Wrap(Card());
        Assert.Equal("message", (string?)e["type"]);
        var a = Assert.Single(e["attachments"]!.AsArray());
        Assert.Equal("application/vnd.microsoft.card.adaptive", (string?)a!["contentType"]);
        Assert.Equal("AdaptiveCard", (string?)a["content"]!["type"]);
    }

    [Fact]
    public void Wrap_MessageEnvelope_IsUnchanged()
    {
        var once = CardEnvelope.Wrap(Card());
        Assert.Same(once, CardEnvelope.Wrap(once));
    }

    [Fact]
    public void Wrap_OtherRoot_Throws() => Assert.Throws<ArgumentException>(() => CardEnvelope.Wrap(new JsonObject { ["type"] = "Other" }));

    [Fact]
    public void ApplySummary_SetsMappedFields_AndIgnoresBlank()
    {
        var e = CardEnvelope.Wrap(Card());
        CardEnvelope.ApplySummary(e, "  ");
        Assert.Null(e["summary"]);
        CardEnvelope.ApplySummary(e, "Page published");
        Assert.Equal("Page published", (string?)e["summary"]);
        Assert.Equal("Page published", (string?)e["attachments"]![0]!["content"]!["fallbackText"]);
    }

    [Fact]
    public void Serialize_CountsUtf8Bytes()
    {
        var (json, bytes) = CardEnvelope.Serialize(CardEnvelope.Wrap(Card(string.Concat(Enumerable.Repeat("é😀", 1000)))));
        Assert.Equal(Encoding.UTF8.GetByteCount(json), bytes);
        Assert.True(bytes > json.Length);
        Assert.DoesNotContain("\\u00E9", json, StringComparison.OrdinalIgnoreCase); // BMP text stays as typed
        Assert.Contains("\\uD83D", json); // emoji are always written as surrogate escapes (12 bytes), and the count includes them
    }

    [Fact]
    public void Serialize_ControlCharacterCountsAsSixBytes()
    {
        var (_, bytes) = CardEnvelope.Serialize(CardEnvelope.Wrap(Card("\u0001")));
        var (_, baseline) = CardEnvelope.Serialize(CardEnvelope.Wrap(Card("")));
        Assert.Equal(6, bytes - baseline);
    }
}
