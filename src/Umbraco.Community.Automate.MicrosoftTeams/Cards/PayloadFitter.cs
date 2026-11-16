using System.Text.Json.Nodes;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>A rendered envelope and the warnings the render produced.</summary>
internal sealed record RenderOutput(JsonObject Envelope, IReadOnlyList<string> Warnings);

/// <summary>The outcome of fitting data into the byte budget. <see cref="Envelope"/> is null only when <see cref="TemplateTooBig"/>.</summary>
internal sealed record FitResult(
    JsonObject? Envelope,
    int Bytes,
    IReadOnlyList<string> ShortenedKeys,
    IReadOnlyList<string> EmptiedKeys,
    IReadOnlyList<string> Warnings,
    bool TemplateTooBig);

/// <summary>
/// Makes a card fit the size limit by shrinking data, never by failing the step (design 4 and 7.4). Order: render as is;
/// render with empty data (over means the template itself is too big); halve the longest value up to 64 times; then
/// empty values longest-first. The empty-data check guarantees the last phase ends in a fit.
/// </summary>
internal static class PayloadFitter
{
    public const int MaxHalvingRounds = 64;

    public static FitResult Fit(
        IReadOnlyDictionary<string, string> data,
        Func<IReadOnlyDictionary<string, string>, RenderOutput> render,
        int maxBytes)
    {
        var values = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        var shortened = new List<string>();
        var emptied = new List<string>();

        var current = Measure(values, render);
        if (current.Bytes <= maxBytes)
        {
            return Result(current, shortened, emptied);
        }

        var blank = values.Keys.ToDictionary(k => k, _ => string.Empty, StringComparer.OrdinalIgnoreCase);
        var empty = Measure(blank, render);
        if (empty.Bytes > maxBytes)
        {
            return new FitResult(null, empty.Bytes, [], [], Dedupe(empty.Output.Warnings), true);
        }

        for (var round = 0; round < MaxHalvingRounds; round++)
        {
            var key = Longest(values);
            if (key is null)
            {
                break;
            }

            var original = values[key];
            var (halved, _) = ValueLimiter.Truncate(original, original.Length / 2);
            if (halved.Length >= original.Length)
            {
                // A value this short can't shrink by halving (the ellipsis adds a character): go straight to emptying.
                break;
            }

            values[key] = halved;
            if (!shortened.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                shortened.Add(key);
            }

            current = Measure(values, render);
            if (current.Bytes <= maxBytes)
            {
                return Result(current, shortened, emptied);
            }
        }

        while (true)
        {
            var key = Longest(values);
            if (key is null)
            {
                // Every value is empty: step 2 proved this fits, so this is only reached by a non-deterministic render.
                break;
            }

            values[key] = string.Empty;
            shortened.RemoveAll(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
            emptied.Add(key);
            current = Measure(values, render);
            if (current.Bytes <= maxBytes)
            {
                return Result(current, shortened, emptied);
            }
        }

        return Result(current, shortened, emptied);
    }

    private static string? Longest(Dictionary<string, string> values) =>
        values.Where(kv => kv.Value.Length > 0)
            .OrderByDescending(kv => kv.Value.Length)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .FirstOrDefault();

    private static (RenderOutput Output, int Bytes) Measure(
        IReadOnlyDictionary<string, string> values,
        Func<IReadOnlyDictionary<string, string>, RenderOutput> render)
    {
        var output = render(values);
        return (output, CardEnvelope.Serialize(output.Envelope).Bytes);
    }

    private static FitResult Result((RenderOutput Output, int Bytes) final, List<string> shortened, List<string> emptied)
    {
        var warnings = Dedupe(final.Output.Warnings).ToList();
        warnings.AddRange(shortened.Select(k => $"Card value '{k}' was shortened to fit."));
        warnings.AddRange(emptied.Select(k => $"Card value '{k}' was removed to fit."));
        return new FitResult(final.Output.Envelope, final.Bytes, shortened, emptied, warnings, false);
    }

    private static IReadOnlyList<string> Dedupe(IEnumerable<string> warnings) => warnings.Distinct(StringComparer.Ordinal).ToList();
}
