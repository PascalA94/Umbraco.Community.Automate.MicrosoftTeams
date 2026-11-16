using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>
/// The publish-time rule shared by both actions (F06): a step with no connection chosen is ambiguous when the workspace
/// allows more than one Teams connection, because it would post to whichever Automate picks.
/// </summary>
internal static class ConnectionChoiceValidator
{
    public const string ConnectionTypeAlias = "community.teams.workflowWebhook";

    public const string ChooseConnection = "Choose which Teams connection this step posts to.";

    private static readonly IReadOnlyList<string> ActionAliases = ["community.teams.postMessage", "community.teams.postCard"];

    public static IReadOnlyList<string> Validate(Guid? stepConnectionId, int teamsConnectionsAllowedInWorkspace) =>
        stepConnectionId is null && teamsConnectionsAllowedInWorkspace > 1 ? [ChooseConnection] : [];

    // The caller resolves Automate's services from its own scope: they are not singleton-safe, so no singleton holds them.
    public static async Task<IReadOnlyList<string>> ValidateAsync(Automation automation, IWorkspaceService workspaces, IConnectionService connections, CancellationToken cancellationToken)
    {
        var steps = automation.Steps.Where(s => ActionAliases.Contains(s.ActionAlias, StringComparer.Ordinal)).ToList();
        if (steps.All(s => s.ConnectionId is not null))
        {
            return [];
        }

        var count = await CountAllowedTeamsConnectionsAsync(automation.WorkspaceId, workspaces, connections, cancellationToken);
        return steps.SelectMany(s => Validate(s.ConnectionId, count)).Distinct(StringComparer.Ordinal).ToList();
    }

    private static async Task<int> CountAllowedTeamsConnectionsAsync(Guid workspaceId, IWorkspaceService workspaces, IConnectionService connections, CancellationToken cancellationToken)
    {
        var workspace = await workspaces.GetWorkspaceAsync(workspaceId, cancellationToken);
        var count = 0;
        foreach (var id in workspace?.AllowedConnections ?? [])
        {
            var connection = await connections.GetConnectionAsync(id, cancellationToken);
            if (string.Equals(connection?.Type, ConnectionTypeAlias, StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
