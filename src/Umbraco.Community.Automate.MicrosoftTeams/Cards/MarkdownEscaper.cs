using System.Text;
using System.Text.RegularExpressions;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>
/// Makes untrusted text show as typed in a Teams text block: Markdown characters are backslash-escaped and auto-links
/// are broken with a zero-width space, so a pasted URL or address is not clickable (design 7.3, F03).
/// </summary>
internal static partial class MarkdownEscaper
{
    private const char ZeroWidthSpace = '\u200B';

    // The spec 7.3 list plus the backtick.
    private const string Special = "\\*_[]()#>-`";

    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            if (Special.Contains(c))
            {
                sb.Append('\\');
            }

            sb.Append(c);
        }

        return BreakLinks(sb.ToString());
    }

    private static string BreakLinks(string text)
    {
        text = SchemeSeparator().Replace(text, $":{ZeroWidthSpace}//");
        text = Www().Replace(text, $"www.{ZeroWidthSpace}");
        return EmailAt().Replace(text, $"{ZeroWidthSpace}@");
    }

    [GeneratedRegex(@"://", RegexOptions.CultureInvariant)]
    private static partial Regex SchemeSeparator();

    [GeneratedRegex(@"www\.", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Www();

    // An address-shaped token: something before the @ and a dotted name after it.
    [GeneratedRegex(@"(?<=\S)@(?=[^\s@]+\.[^\s@])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex EmailAt();
}
