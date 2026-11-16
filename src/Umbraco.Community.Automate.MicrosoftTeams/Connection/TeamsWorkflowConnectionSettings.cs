using Umbraco.Automate.Core.Settings;

namespace Umbraco.Community.Automate.MicrosoftTeams.Connection;

/// <summary>
/// Settings for the <c>community.teams.workflowWebhook</c> connection. Nothing here is a secret: the webhook URL
/// lives in server configuration and is read through the credential store, never stored on the connection.
/// </summary>
public sealed class TeamsWorkflowConnectionSettings
{
    /// <summary>The name of a package-owned credential in configuration (not the URL).</summary>
    // No bindings and not sensitive: the name picks a server-side value, so run data must not steer it.
    [Field(Label = "Credential name", Description = "The name of the credential configured under Umbraco:Automate:MicrosoftTeams:Credentials. This is a name, not the webhook URL.", SupportsBindings = false, IsSensitive = false, SortOrder = 1)]
    public string? CredentialName { get; set; }

    /// <summary>The Adaptive Card version declared on cards this connection posts.</summary>
    [Field(
        Label = "Default card version",
        Description = "The Adaptive Card version used when a card does not declare one.",
        EditorUiAlias = "Umb.PropertyEditorUi.Dropdown",
        EditorConfig = """[{ "alias": "items", "value": ["1.2", "1.3", "1.4", "1.5"] }]""",
        SupportsBindings = false,
        Group = "Advanced",
        SortOrder = 2)]
    public string DefaultCardVersion { get; set; } = "1.4";
}
