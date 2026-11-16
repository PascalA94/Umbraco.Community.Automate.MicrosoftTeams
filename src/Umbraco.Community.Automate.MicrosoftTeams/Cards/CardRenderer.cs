using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>How one card render behaves.</summary>
internal sealed record CardRenderOptions(bool EscapeMarkdown, string DefaultCardVersion, string? Summary, bool AllowExternalLinks);

/// <summary>A rendered envelope and the warnings the render produced.</summary>
internal sealed record CardRenderResult(JsonObject Envelope, IReadOnlyList<string> Warnings)
{
    public RenderOutput ToRenderOutput() => new(Envelope, Warnings);
}

/// <summary>Fills <c>{{key}}</c> placeholders in a card template without ever producing invalid JSON.</summary>
internal interface ICardRenderer
{
    /// <summary>
    /// Renders a template (an AdaptiveCard or a message envelope) with <paramref name="data"/>. The link checker is passed in,
    /// not resolved here, because its site-host lookup is async and the payload fitter renders many times.
    /// </summary>
    CardRenderResult Render(JsonNode template, IReadOnlyDictionary<string, string> data, CardRenderOptions options, LinkChecker links);
}

/// <inheritdoc />
internal sealed partial class CardRenderer : ICardRenderer
{
    /// <summary>Exposed so a test can pin the match timeout (plan review P7).</summary>
    internal static Regex PlaceholderPattern => Placeholder();

    public CardRenderResult Render(JsonNode template, IReadOnlyDictionary<string, string> data, CardRenderOptions options, LinkChecker links)
    {
        if (template is not JsonObject)
        {
            throw new ArgumentException("Expected an AdaptiveCard or a message envelope.", nameof(template));
        }

        // The template is cached and shared, so it is never touched: all work happens on a clone.
        var root = (JsonObject)template.DeepClone();
        var run = new Run(data, options, links);
        run.WalkObject(root, isRoot: true);

        var envelope = CardEnvelope.Wrap(root);
        SetMissingVersions(envelope, options.DefaultCardVersion);
        CardEnvelope.ApplySummary(envelope, options.Summary);

        run.Warnings.AddRange(PersonalDataKeys.Warnings(run.Data.Keys));
        return new CardRenderResult(envelope, run.Warnings.Distinct(StringComparer.Ordinal).ToList());
    }

    private static void SetMissingVersions(JsonObject envelope, string defaultVersion)
    {
        if (envelope["attachments"] is not JsonArray attachments)
        {
            return;
        }

        foreach (var attachment in attachments.OfType<JsonObject>())
        {
            if (attachment["content"] is JsonObject content
                && string.Equals(Text(attachment["contentType"]), TeamsWorkflowsFacts.AdaptiveCardContentType, StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(Text(content["version"])))
            {
                content["version"] = defaultVersion;
            }
        }
    }

    private static string? Text(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    // One walk for the whole tree, so every attachment of a full envelope gets the same rules (security review 4.3).
    private sealed class Run
    {
        private readonly CardRenderOptions _options;
        private readonly LinkChecker _links;

        public Run(IReadOnlyDictionary<string, string> data, CardRenderOptions options, LinkChecker links)
        {
            _options = options;
            _links = links;
            // A lone surrogate can't be written as UTF-8 JSON and would throw at serialisation, so it becomes U+FFFD.
            Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in data)
            {
                Data[key] = ValueLimiter.ReplaceLoneSurrogates(value);
            }
        }

        public Dictionary<string, string> Data { get; }

        public List<string> Warnings { get; } = [];

        /// <summary>Returns true when the object itself must be removed from its parent.</summary>
        public bool WalkObject(JsonObject obj, bool isRoot)
        {
            foreach (var (name, value) in obj.ToList())
            {
                switch (value)
                {
                    case JsonValue v when v.TryGetValue<string>(out var text):
                        if (UrlProperties.IsUrlProperty(name, obj))
                        {
                            if (!CheckUrl(obj, name, text, isRoot))
                            {
                                return true;
                            }
                        }
                        else
                        {
                            obj[name] = Substitute(text, _options.EscapeMarkdown);
                        }

                        break;
                    case JsonObject child:
                        if (WalkObject(child, isRoot: false))
                        {
                            obj.Remove(name);
                        }

                        break;
                    case JsonArray array:
                        WalkArray(array);
                        break;
                }
            }

            var type = Text(obj["type"]);
            if (!isRoot
                && ((type == "ActionSet" && obj["actions"] is JsonArray { Count: 0 })
                    || (type == "Media" && obj["sources"] is JsonArray { Count: 0 })))
            {
                return true;
            }

            if (type != "ActionSet" && obj["actions"] is JsonArray { Count: 0 })
            {
                obj.Remove("actions");
            }

            return false;
        }

        private void WalkArray(JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                switch (array[i])
                {
                    case JsonObject child:
                        if (WalkObject(child, isRoot: false))
                        {
                            array.RemoveAt(i);
                        }

                        break;
                    case JsonArray nested:
                        WalkArray(nested);
                        break;
                    case JsonValue v when v.TryGetValue<string>(out var text):
                        array[i] = Substitute(text, _options.EscapeMarkdown);
                        break;
                }
            }
        }

        // False means the whole object must go: a bad `url` takes its action, image or source with it. Any other URL
        // property (iconUrl, poster, backgroundImage) is just dropped, which keeps the element.
        private bool CheckUrl(JsonObject obj, string name, string template, bool isRoot)
        {
            var url = Substitute(template, escape: false);
            var decision = _links.Check(url, _options.AllowExternalLinks);
            if (decision.IsAllowed)
            {
                obj[name] = new Uri(url.Trim(), UriKind.Absolute).AbsoluteUri;
                return true;
            }

            // The reason is a fixed phrase: never the URL, its host or the data value.
            var removesObject = name.Equals("url", StringComparison.OrdinalIgnoreCase) && !isRoot;
            var label = removesObject ? Text(obj["type"]) ?? "card" : name;
            Warnings.Add($"The '{label}' link was removed: {decision.Reason}.");
            if (removesObject)
            {
                return false;
            }

            obj.Remove(name);
            return true;
        }

        // One Regex.Replace pass: the output is never rescanned, so a value that itself holds {{x}} stays literal.
        private string Substitute(string template, bool escape) =>
            Placeholder().Replace(template, match =>
            {
                var key = match.Groups[1].Value;
                if (Data.TryGetValue(key, out var value))
                {
                    return escape ? MarkdownEscaper.Escape(value) : value;
                }

                Warnings.Add($"Unknown card key: {key}");
                return string.Empty;
            });
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Placeholder();
}
