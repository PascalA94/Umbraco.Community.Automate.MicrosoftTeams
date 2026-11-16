using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>Builds the Post message card as a JSON node tree, so bound data can never break the JSON (design 6.2 and 7.3).</summary>
internal static partial class MessageCardBuilder
{
    private const int MaxLinkLabelLength = 100;

    /// <summary>
    /// Escape order (plan review P9): split Title and Message into literal segments and <c>{{key}}</c> placeholders first.
    /// In Plain mode each literal segment is escaped; in Markdown mode literals are left alone. Each placeholder is then
    /// replaced once with its escaped value, so no character is escaped twice and a key such as <c>{{form_name}}</c> still matches.
    /// </summary>
    public static RenderOutput Build(
        PostMessageSettings settings,
        IReadOnlyDictionary<string, string> data,
        string cardVersion,
        LinkChecker links)
    {
        var warnings = new List<string>();
        var lookup = new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
        var plain = settings.Formatting == MessageFormatting.PlainText;

        var body = new JsonArray();
        var container = new JsonObject { ["type"] = "Container", ["items"] = body };
        if (StyleName(settings.Style) is { } style)
        {
            container["style"] = style;
        }

        var title = Substitute(settings.Title, lookup, plain, warnings);
        if (title.Length > 0)
        {
            body.Add(new JsonObject { ["type"] = "TextBlock", ["text"] = title, ["weight"] = "Bolder", ["size"] = "Medium", ["wrap"] = true });
        }

        body.Add(new JsonObject { ["type"] = "TextBlock", ["text"] = Substitute(settings.Message, lookup, plain, warnings), ["wrap"] = true });

        if (lookup.Count > 0)
        {
            var facts = new JsonArray();
            foreach (var (key, value) in lookup.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                // Values are always escaped, whatever the formatting: Data is where untrusted values belong.
                facts.Add(new JsonObject { ["title"] = MarkdownEscaper.Escape(key), ["value"] = MarkdownEscaper.Escape(value) });
            }

            body.Add(new JsonObject { ["type"] = "FactSet", ["facts"] = facts });
        }

        var card = new JsonObject
        {
            ["type"] = "AdaptiveCard",
            ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
            ["version"] = cardVersion,
            ["body"] = new JsonArray(container),
        };

        if (!string.IsNullOrWhiteSpace(settings.LinkUrl))
        {
            var decision = links.Check(settings.LinkUrl, settings.AllowExternalLinks);
            if (decision.IsAllowed)
            {
                var label = ValueLimiter.Truncate(settings.LinkLabel?.Trim(), MaxLinkLabelLength).Value;
                card["actions"] = new JsonArray(new JsonObject
                {
                    ["type"] = "Action.OpenUrl",
                    ["title"] = label.Length > 0 ? label : "Open",
                    ["url"] = new Uri(settings.LinkUrl.Trim(), UriKind.Absolute).AbsoluteUri,
                });
            }
            else
            {
                // The reason is a fixed phrase: never the URL or its host.
                warnings.Add($"The link button was removed: {decision.Reason}.");
            }
        }

        return new RenderOutput(CardEnvelope.Wrap(card), warnings);
    }

    private static string Substitute(string? template, Dictionary<string, string> data, bool escapeLiterals, List<string> warnings)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        var sb = new System.Text.StringBuilder();
        var last = 0;
        foreach (Match match in Placeholder().Matches(template))
        {
            AppendLiteral(sb, template[last..match.Index], escapeLiterals);
            var key = match.Groups[1].Value;
            if (data.TryGetValue(key, out var value))
            {
                sb.Append(MarkdownEscaper.Escape(value));
            }
            else
            {
                warnings.Add($"Unknown card key: {key}");
            }

            last = match.Index + match.Length;
        }

        AppendLiteral(sb, template[last..], escapeLiterals);
        return sb.ToString();
    }

    private static void AppendLiteral(System.Text.StringBuilder sb, string literal, bool escape) =>
        sb.Append(escape ? MarkdownEscaper.Escape(literal) : literal);

    private static string? StyleName(MessageStyle style) => style switch
    {
        MessageStyle.Good => "good",
        MessageStyle.Warning => "warning",
        MessageStyle.Attention => "attention",
        _ => null,
    };

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Placeholder();
}
