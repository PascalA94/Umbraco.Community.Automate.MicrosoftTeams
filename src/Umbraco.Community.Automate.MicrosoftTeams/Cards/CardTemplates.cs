using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>
/// The four built-in cards, kept as embedded JSON data files so a change found at the Live gate (S5) is an edit, not code.
/// Key names are frozen once released, like aliases (design 6.3).
/// </summary>
internal sealed class CardTemplates
{
    private sealed record Entry(string Resource, string DisplayName, string[] Required, string[] Optional);

    // An instance field, not a static: the architecture guard allows no static field beyond constants and a short immutable list.
    private readonly FrozenDictionary<CardSource, Entry> _entries = new Dictionary<CardSource, Entry>
    {
        [CardSource.ContentPublished] = new("content-published.json", "Content published", ["title", "url"], ["contentType", "editor", "publishedAt"]),
        [CardSource.ApprovalNeeded] = new("approval-needed.json", "Approval needed", ["title", "url"], ["editor", "requestedAt", "notes"]),
        [CardSource.FormSubmitted] = new("form-submitted.json", "Form submitted", ["formName", "url"], ["entryId", "submittedAt"]),
        [CardSource.Simple] = new("simple.json", "Simple", ["title", "text"], ["url", "linkLabel"]),
    }.ToFrozenDictionary();

    // Parsed on first use and cached (plan review P7); every caller gets a clone, so nobody can change the cache.
    private readonly Dictionary<CardSource, Lazy<JsonNode>> _parsed;

    public CardTemplates()
    {
        _parsed = _entries.ToDictionary(e => e.Key, e => new Lazy<JsonNode>(() => Load(e.Value.Resource)));
    }

    public JsonNode Get(CardSource source) =>
        _parsed.TryGetValue(source, out var template)
            ? template.Value.DeepClone()
            : throw new ArgumentException($"'{source}' is not a built-in template.", nameof(source));

    public IReadOnlyList<string> RequiredKeys(CardSource source) => _entries.TryGetValue(source, out var e) ? e.Required : [];

    /// <summary>Required keys first, then the optional ones, in the spec's order.</summary>
    public IReadOnlyList<string> AllKeys(CardSource source) => _entries.TryGetValue(source, out var e) ? [.. e.Required, .. e.Optional] : [];

    public string DisplayName(CardSource source) => _entries.TryGetValue(source, out var e) ? e.DisplayName : source.ToString();

    /// <summary>Built-in templates always apply the link host rule, whatever the author ticked (F04).</summary>
    public static bool EffectiveAllowExternalLinks(CardSource source, bool requested) => requested && source == CardSource.Custom;

    private static JsonNode Load(string resource)
    {
        var name = $"Umbraco.Community.Automate.MicrosoftTeams.Cards.Templates.{resource}";
        using var stream = typeof(CardTemplates).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"The embedded template '{resource}' is missing.");
        return JsonNode.Parse(stream)!;
    }
}
