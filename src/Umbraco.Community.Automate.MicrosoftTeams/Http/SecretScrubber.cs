using System.Text.RegularExpressions;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>Makes text from Teams safe to show in a step failure: no control or bidi characters, no URLs, no signatures, short.</summary>
internal static partial class SecretScrubber
{
    public static string Scrub(string? text, int maxLength = 200, IEnumerable<string>? hostPatterns = null, string? knownHost = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Strip first so a control character cannot split a URL or "sig=" and slip past the redaction.
        var redacted = Url().Replace(ValueLimiter.StripControlAndBidi(text), "[redacted]");
        redacted = Sig().Replace(redacted, "[redacted]");
        var patterns = hostPatterns?.ToList() ?? [];
        if (patterns.Count > 0 || !string.IsNullOrWhiteSpace(knownHost))
        {
            // A body can echo the host and path without the scheme, so a URL match alone misses it.
            redacted = HostLike().Replace(redacted, m =>
            {
                var host = m.Groups["host"].Value;
                return (!string.IsNullOrWhiteSpace(knownHost) && HostNames.Normalise(host) == HostNames.Normalise(knownHost))
                    || HostNames.Matches(host, patterns)
                    ? "[redacted]"
                    : m.Value;
            });
        }

        if (redacted.Length > maxLength)
        {
            // Don't cut a surrogate pair in half.
            var cut = maxLength;
            if (char.IsHighSurrogate(redacted[cut - 1]))
            {
                cut--;
            }

            redacted = redacted[..cut];
        }

        return redacted;
    }

    // Also matches the JSON-escaped form (https:\/\/host\/path), which a JSON error body echoes.
    [GeneratedRegex(@"https?:\\?/\\?/\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Url();

    [GeneratedRegex(@"sig(?:=|%3d|\\u003d)[^&\s""]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex Sig();

    // A dotted name with an optional port and an optional path, query or fragment to the next space.
    [GeneratedRegex(@"(?<host>[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?)+)(?::\d+)?(?:[/?#]\S*)?", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex HostLike();
}
