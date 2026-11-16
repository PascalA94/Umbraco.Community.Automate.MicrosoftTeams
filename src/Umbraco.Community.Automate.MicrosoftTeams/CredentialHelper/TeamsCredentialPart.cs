namespace Umbraco.Community.Automate.MicrosoftTeams.Credentials;

/// <summary>
/// The credential values the package reads from configuration for one credential name.
/// </summary>
// Public only because the contract test classes must be public for xUnit v2 and name this enum as a type argument.
public enum TeamsCredentialPart
{
    /// <summary>The whole Workflows incoming-webhook URL.</summary>
    WebhookUrl,
}
