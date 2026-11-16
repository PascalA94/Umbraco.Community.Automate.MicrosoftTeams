using System.Globalization;
using System.Text;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>Shortens bound values without ever failing the step (F01).</summary>
internal static class ValueLimiter
{
    private const string Ellipsis = "…";

    /// <summary>
    /// Replaces each unpaired surrogate with U+FFFD. Such text can't be written as UTF-8 JSON, so left alone it would throw
    /// when the card is serialised and fail the step over a data problem (F01).
    /// </summary>
    public static string ReplaceLoneSurrogates(string value)
    {
        StringBuilder? sb = null;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                sb?.Append(c).Append(value[i + 1]);
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                sb ??= new StringBuilder(value.Length).Append(value, 0, i);
                sb.Append('\uFFFD');
            }
            else
            {
                sb?.Append(c);
            }
        }

        return sb?.ToString() ?? value;
    }

    /// <summary>
    /// Drops control and bidirectional-override characters. They cost up to six bytes each once JSON-escaped and can reorder
    /// the text a reader sees in a notification preview.
    /// </summary>
    public static string StripControlAndBidi(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (!char.IsControl(c) && !IsBidi(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    internal static bool IsBidi(char c) =>
        c is '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');

    /// <summary>Cuts on a grapheme boundary at or below <paramref name="maxLength"/> UTF-16 chars, then appends an ellipsis.</summary>
    public static (string Value, bool Truncated) Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (string.Empty, false);
        }

        value = ReplaceLoneSurrogates(value);
        if (value.Length <= maxLength)
        {
            return (value, false);
        }

        var sb = new StringBuilder(Math.Max(maxLength, 0) + 1);
        var elements = StringInfo.GetTextElementEnumerator(value);
        while (elements.MoveNext())
        {
            var element = (string)elements.Current;
            if (sb.Length + element.Length > maxLength)
            {
                break;
            }

            sb.Append(element);
        }

        return (sb.Append(Ellipsis).ToString(), true);
    }
}
