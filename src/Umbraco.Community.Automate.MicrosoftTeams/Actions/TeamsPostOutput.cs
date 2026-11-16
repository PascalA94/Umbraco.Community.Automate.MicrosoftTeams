using Umbraco.Community.Automate.MicrosoftTeams.Http;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>What a Teams step reports. No URL, host or response body, ever.</summary>
public sealed class TeamsPostOutput
{
    /// <summary>The HTTP status Teams answered with.</summary>
    public int StatusCode { get; set; }

    /// <summary>True when Teams accepted the post (202).</summary>
    public bool Accepted { get; set; }

    /// <summary>The size of the posted payload in UTF-8 bytes.</summary>
    public int PayloadBytes { get; set; }

    /// <summary>Data problems fixed in the payload, naming keys, never values.</summary>
    public string[] Warnings { get; set; } = [];

    // Host-naming test warnings are for the admin-only connection test and are never copied here.
    internal static TeamsPostOutput From(TeamsPostResult result, IEnumerable<string> extraWarnings) => new()
    {
        StatusCode = result.StatusCode,
        Accepted = result.Accepted,
        PayloadBytes = result.PayloadBytes,
        Warnings = [.. result.Warnings.Concat(extraWarnings).Distinct(StringComparer.Ordinal)],
    };
}
