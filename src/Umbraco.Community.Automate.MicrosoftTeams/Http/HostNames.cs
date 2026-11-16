using System.Globalization;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>Host comparison shared by the webhook URL checks and the link host rule.</summary>
internal static class HostNames
{
    private static readonly IdnMapping Idn = new();

    /// <summary>Lower case, one trailing dot stripped, and the <c>xn--</c> form for Unicode hosts, so look-alikes compare as what they are.</summary>
    public static string Normalise(string host)
    {
        var value = host.Trim();
        if (value.EndsWith('.'))
        {
            value = value[..^1];
        }

        try
        {
            value = Idn.GetAscii(value);
        }
        catch (ArgumentException)
        {
            // Not convertible: keep the text and let IsDnsName reject it.
        }

        return value.ToLowerInvariant();
    }

    /// <summary>True for letters, digits, hyphens and dots, at most 253 characters. Anything else is never echoed back.</summary>
    public static bool IsDnsName(string host)
    {
        if (host.Length is 0 or > 253)
        {
            return false;
        }

        foreach (var c in host)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The wildcard rule: <c>*.x.com</c> matches subdomains only, never <c>x.com</c>. Case-insensitive.</summary>
    public static bool Matches(string host, IEnumerable<string> patterns)
    {
        var normalised = Normalise(host);
        foreach (var raw in patterns)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var pattern = raw.Trim().ToLowerInvariant();
            if (pattern.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = pattern[1..];
                if (normalised.Length > suffix.Length && normalised.EndsWith(suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (string.Equals(normalised, Normalise(pattern), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
