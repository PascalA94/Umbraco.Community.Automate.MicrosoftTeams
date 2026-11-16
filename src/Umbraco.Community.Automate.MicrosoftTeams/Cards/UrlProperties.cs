using System.Text.Json.Nodes;

namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>
/// The explicit, case-insensitive list of Adaptive Card properties that hold a URL (design 7.3, F05): <c>url</c>, anything
/// ending <c>Url</c>, <c>backgroundImage</c> in its string form, and <c>poster</c>. <c>sources[].url</c> and
/// <c>selectAction.url</c> are plain <c>url</c> properties on a nested object. A test walks the stored 1.5 schema and
/// fails when a URI-format property is missing from this list.
/// </summary>
internal static class UrlProperties
{
    public static bool IsUrlProperty(string propertyName, JsonNode parent) =>
        parent is JsonObject
        && (propertyName.EndsWith("url", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("backgroundImage", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("poster", StringComparison.OrdinalIgnoreCase));
}
