using System.Text.Json.Nodes;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class PayloadFitterTests
{
    private const int Max = 28000;

    // A FactSet of every key, serialised through CardEnvelope, so the byte count is the real one.
    private static RenderOutput Render(IReadOnlyDictionary<string, string> data, Func<string, string>? escape = null, params string[] warnings)
    {
        escape ??= v => v;
        var facts = new JsonArray();
        foreach (var (k, v) in data.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            facts.Add(new JsonObject { ["title"] = k, ["value"] = escape(v) });
        }

        var card = new JsonObject
        {
            ["type"] = "AdaptiveCard",
            ["version"] = "1.4",
            ["body"] = new JsonArray(new JsonObject { ["type"] = "FactSet", ["facts"] = facts }),
        };
        return new RenderOutput(CardEnvelope.Wrap(card), warnings);
    }

    private static Dictionary<string, string> Rows(int n, int len, char c = 'x') =>
        Enumerable.Range(0, n).ToDictionary(i => $"k{i:D2}", _ => new string(c, len));

    [Fact]
    public void Fit_100KbValue_SentWithinBudget()
    {
        var r = PayloadFitter.Fit(new Dictionary<string, string> { ["notes"] = new string('n', 100_000) }, d => Render(d), Max);
        Assert.Equal(["notes"], r.ShortenedKeys);
        Assert.True(r.Bytes <= Max);
        Assert.False(r.TemplateTooBig);
    }

    [Fact]
    public void Fit_TwentyKeysOf2000_FitsAndNamesShortenedKeys()
    {
        var r = PayloadFitter.Fit(Rows(20, 2000), d => Render(d), Max);
        Assert.True(r.Bytes <= Max);
        Assert.NotEmpty(r.ShortenedKeys);
        Assert.All(r.ShortenedKeys, k => Assert.Contains($"Card value '{k}' was shortened to fit.", r.Warnings));
    }

    [Fact]
    public void Fit_EmojiAndControlChars_CountsEscapedUtf8Bytes()
    {
        var data = new Dictionary<string, string> { ["a"] = new string('\u0001', 6000), ["b"] = string.Concat(Enumerable.Repeat("😀", 2000)) };
        var r = PayloadFitter.Fit(data, d => Render(d), Max);
        Assert.True(r.Bytes <= Max);
        Assert.Equal(r.Bytes, CardEnvelope.Serialize(r.Envelope!).Bytes);
    }

    [Fact]
    public void Fit_50RowsOf2000SixByteEscapedChars_Fits()
    {
        var r = PayloadFitter.Fit(Rows(50, 2000, '\u0001'), d => Render(d), Max);
        Assert.True(r.Bytes <= Max);
        Assert.False(r.TemplateTooBig);
    }

    [Fact]
    public void Fit_EmptyingPhase_EmptiesOnlyTheLongestValue()
    {
        // Halving can't shrink a value of two characters (the ellipsis would make it no shorter), so it stops at once and
        // the emptying phase starts with unequal lengths. Each \u0001 costs six bytes once escaped.
        var data = new Dictionary<string, string> { ["long"] = "\u0001\u0001" };
        foreach (var key in new[] { "s1", "s2", "s3", "s4", "s5" })
        {
            data[key] = "\u0001";
        }

        var full = CardEnvelope.Serialize(Render(data).Envelope).Bytes;
        var r = PayloadFitter.Fit(data, d => Render(d), full - 1);

        Assert.Equal(["long"], r.EmptiedKeys);
        Assert.Empty(r.ShortenedKeys);
        Assert.True(r.Bytes <= full - 1);
        var facts = r.Envelope!["attachments"]![0]!["content"]!["body"]![0]!["facts"]!.AsArray();
        Assert.Equal(string.Empty, facts.Single(f => f!["title"]!.GetValue<string>() == "long")!["value"]!.GetValue<string>());
        Assert.All(new[] { "s1", "s2", "s3", "s4", "s5" }, k => Assert.Equal("\u0001", facts.Single(f => f!["title"]!.GetValue<string>() == k)!["value"]!.GetValue<string>()));
    }

    [Fact]
    public void Fit_64RoundsExhausted_EmptiesOnlyTheLongestRemaining()
    {
        // One 20,000 value and many 100-169 character ones, so the 64 halving rounds run out with unequal lengths left.
        var data = Enumerable.Range(0, 70).ToDictionary(i => $"k{i:D2}", i => new string('\u0001', 100 + i));
        data["big"] = new string('\u0001', 20_000);

        // Calibration run: a budget of the empty card empties everything, and the render log shows the state after round 64.
        var states = new List<(Dictionary<string, string> Data, int Bytes)>();
        var blankBytes = CardEnvelope.Serialize(Render(data.ToDictionary(kv => kv.Key, _ => string.Empty)).Envelope).Bytes;
        PayloadFitter.Fit(data, d =>
        {
            var o = Render(d);
            states.Add((new Dictionary<string, string>(d), CardEnvelope.Serialize(o.Envelope).Bytes));
            return o;
        }, blankBytes);
        var afterHalving = states[2 + PayloadFitter.MaxHalvingRounds - 1];
        var ranked = afterHalving.Data.OrderByDescending(kv => kv.Value.Length).ToList();
        Assert.True(ranked[0].Value.Length > ranked[1].Value.Length, "precondition: a unique longest value when halving ends");
        var longest = ranked[0].Key;

        var r = PayloadFitter.Fit(data, d => Render(d), afterHalving.Bytes - 1);

        Assert.Equal([longest], r.EmptiedKeys);
        var facts = r.Envelope!["attachments"]![0]!["content"]!["body"]![0]!["facts"]!.AsArray();
        Assert.Equal(string.Empty, facts.Single(f => f!["title"]!.GetValue<string>() == longest)!["value"]!.GetValue<string>());
        Assert.All(facts.Where(f => f!["title"]!.GetValue<string>() != longest), f => Assert.NotEqual(string.Empty, f!["value"]!.GetValue<string>()));
    }

    [Fact]
    public void Fit_EscapeExpansionNearLimit_Fits()
    {
        // 2,000 "*" become 4,000 chars once escaped: values sized to land just over the limit after escaping.
        string Escape(string v) => v.Replace("*", "\\*");
        var data = Rows(7, 2000, '*');
        Assert.True(CardEnvelope.Serialize(Render(data, Escape).Envelope).Bytes > Max);
        var r = PayloadFitter.Fit(data, d => Render(d, Escape), Max);
        Assert.True(r.Bytes <= Max);
        Assert.False(r.TemplateTooBig);
    }

    [Fact]
    public void Fit_AllEmptyFits_AlwaysReturnsFittingEnvelope()
    {
        var inputs = new[]
        {
            Rows(50, 2000, '\u0001'),
            Rows(20, 2000),
            new Dictionary<string, string> { ["a"] = new string('é', 28_000) },
            new Dictionary<string, string> { ["a"] = new string('x', 1), ["b"] = new string('y', 2) },
        };
        foreach (var data in inputs)
        {
            var r = PayloadFitter.Fit(data, d => Render(d), Max);
            Assert.NotNull(r.Envelope);
            Assert.True(r.Bytes <= Max);
        }
    }

    [Fact]
    public void Fit_WarningsFromFinalRender_Deduplicated()
    {
        var r = PayloadFitter.Fit(Rows(3, 20_000), d => Render(d, null, "Unknown card key: x", "Unknown card key: x"), Max);
        Assert.Single(r.Warnings, w => w == "Unknown card key: x");
    }

    [Fact]
    public void Fit_EmptyDataTemplateTooBig_TemplateTooBigTrue()
    {
        var r = PayloadFitter.Fit(
            new Dictionary<string, string> { ["a"] = "x" },
            d => Render(d.ToDictionary(kv => new string('k', 40_000) + kv.Key, kv => kv.Value)),
            Max);
        Assert.True(r.TemplateTooBig);
        Assert.Null(r.Envelope);
        Assert.True(r.Bytes > Max);
    }

    [Fact]
    public void Fit_AlreadyFits_RendersOnce()
    {
        var renders = 0;
        var r = PayloadFitter.Fit(new Dictionary<string, string> { ["a"] = "small" }, d => { renders++; return Render(d); }, Max);
        Assert.Equal(1, renders);
        Assert.Empty(r.ShortenedKeys);
        Assert.Empty(r.EmptiedKeys);
    }
}
