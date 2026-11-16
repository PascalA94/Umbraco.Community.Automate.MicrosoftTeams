using System.Collections.Frozen;
using System.Text.RegularExpressions;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>An advisory hint when a card key looks like it carries personal data (F13). Never blocks.</summary>
internal static partial class PersonalDataKeys
{
    private static readonly FrozenSet<string> Words = new[] { "email", "phone", "mobile", "address", "postcode" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<string> WholeKeys = new[] { "name", "firstname", "lastname", "fullname" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> Warnings(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (LooksPersonal(key))
            {
                yield return $"Card key '{key}' may hold personal data. See the GDPR notes.";
            }
        }
    }

    private static bool LooksPersonal(string key)
    {
        if (WholeKeys.Contains(key.Trim()))
        {
            return true;
        }

        // Split on case changes, underscores, hyphens and dots, so "contactEmail" and "phone_number" match but "formName" does not.
        foreach (Match word in WordPattern().Matches(key))
        {
            if (Words.Contains(word.Value))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z]?[a-z]+|[0-9]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex WordPattern();
}
