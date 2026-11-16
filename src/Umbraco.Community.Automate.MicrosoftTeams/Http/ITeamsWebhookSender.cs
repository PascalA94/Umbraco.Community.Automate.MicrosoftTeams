using System.Text.Json.Nodes;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>Posts an envelope to the webhook behind a credential name. The URL never leaves the implementation.</summary>
internal interface ITeamsWebhookSender
{
    /// <summary>Takes the credential name, never a URL.</summary>
    Task<TeamsPostResult> PostAsync(string? credentialName, JsonObject envelope, CancellationToken cancellationToken);
}
