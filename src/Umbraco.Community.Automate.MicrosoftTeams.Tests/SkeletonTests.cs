using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Connection;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class SkeletonTests
{
    [Fact]
    public void FrozenAliases_AreExactlyAsReleased()
    {
        var connection = typeof(TeamsWorkflowConnectionType).GetCustomAttributes(typeof(ConnectionTypeAttribute), false).Cast<ConnectionTypeAttribute>().Single();
        Assert.Equal("community.teams.workflowWebhook", connection.Alias);
        var action = typeof(PostMessageAction).GetCustomAttributes(typeof(ActionAttribute), false).Cast<ActionAttribute>().Single();
        Assert.Equal("community.teams.postMessage", action.Alias);
        Assert.Equal("community.teams.workflowWebhook", action.ConnectionTypeAlias);
    }
}
