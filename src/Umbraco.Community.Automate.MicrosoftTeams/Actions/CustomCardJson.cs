using System.Text.Json;
using System.Text.Json.Nodes;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>Parses and checks the author's card JSON, once, for both the save check and the run (design 6.3).</summary>
internal static class CustomCardJson
{
    public const string Required = "Custom card JSON is required.";

    public const string WrongRoot = "Custom card JSON must be an AdaptiveCard, or a message with an attachments array.";

    /// <summary>Returns the card, or null with a message that names no part of the JSON's content.</summary>
    public static JsonObject? TryParse(string? json, out string? error)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            error = Required;
            return null;
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (Exception e) when (e is JsonException or ArgumentException)
        {
            // The line number only: a parser message can quote the text around the fault.
            var line = e is JsonException { LineNumber: { } n } ? $" (line {n + 1})" : string.Empty;
            error = $"Custom card JSON isn't valid JSON{line}.";
            return null;
        }

        if (node is JsonObject root && IsCardOrEnvelope(root))
        {
            error = null;
            return root;
        }

        error = WrongRoot;
        return null;
    }

    private static bool IsCardOrEnvelope(JsonObject root) => Type(root) switch
    {
        "AdaptiveCard" => true,
        "message" => root["attachments"] is JsonArray,
        _ => false,
    };

    private static string? Type(JsonObject node) => node["type"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>True when any element or action in the tree is an <c>Action.Submit</c>, which Workflows doesn't support.</summary>
    public static bool HasActionSubmit(JsonNode? node) => node switch
    {
        JsonObject o => Type(o) == "Action.Submit" || o.Any(kv => HasActionSubmit(kv.Value)),
        JsonArray a => a.Any(HasActionSubmit),
        _ => false,
    };
}
