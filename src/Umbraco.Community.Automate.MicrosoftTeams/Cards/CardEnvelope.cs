using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>Wraps a card in the message envelope Teams expects, sets its summary and measures it in UTF-8 bytes.</summary>
internal static class CardEnvelope
{
    // Relaxed escaping keeps non-ASCII text as typed: the default encoder writes every CJK character as six bytes,
    // which would eat the 28 KB budget. This is a JSON body, never HTML, so the HTML-sensitive characters are fine.
    private static readonly JsonSerializerOptions Compact = new()
    {
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonObject Wrap(JsonObject cardOrEnvelope)
    {
        var type = (cardOrEnvelope["type"] as JsonValue)?.GetValue<string>();
        if (string.Equals(type, TeamsWorkflowsFacts.EnvelopeType, StringComparison.Ordinal))
        {
            return cardOrEnvelope;
        }

        if (string.Equals(type, "AdaptiveCard", StringComparison.Ordinal))
        {
            return new JsonObject
            {
                ["type"] = TeamsWorkflowsFacts.EnvelopeType,
                ["attachments"] = new JsonArray(new JsonObject
                {
                    ["contentType"] = TeamsWorkflowsFacts.AdaptiveCardContentType,
                    ["content"] = cardOrEnvelope,
                }),
            };
        }

        throw new ArgumentException("Expected an AdaptiveCard or a message envelope.", nameof(cardOrEnvelope));
    }

    /// <summary>
    /// Sets both the envelope's <c>summary</c> and each Adaptive Card's <c>fallbackText</c>, because which one Teams uses
    /// for the notification preview is unverified until the Live gate (S5). A blank summary changes nothing.
    /// </summary>
    public static void ApplySummary(JsonObject envelope, string? summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            return;
        }

        envelope["summary"] = summary;
        if (envelope["attachments"] is JsonArray attachments)
        {
            foreach (var attachment in attachments.OfType<JsonObject>())
            {
                if (attachment["content"] is JsonObject content
                    && string.Equals((attachment["contentType"] as JsonValue)?.GetValue<string>(), TeamsWorkflowsFacts.AdaptiveCardContentType, StringComparison.Ordinal))
                {
                    content["fallbackText"] = summary;
                }
            }
        }
    }

    public static (string Json, int Bytes) Serialize(JsonObject envelope)
    {
        var json = envelope.ToJsonString(Compact);
        return (json, Encoding.UTF8.GetByteCount(json));
    }
}
