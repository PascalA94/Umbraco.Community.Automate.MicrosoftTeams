namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>What a post did. <see cref="TestWarnings"/> may name the host and is for the admin-only connection test; actions never read it.</summary>
internal sealed record TeamsPostResult(
    bool Accepted,
    int StatusCode,
    int PayloadBytes,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> TestWarnings,
    TeamsFailure? Failure);
