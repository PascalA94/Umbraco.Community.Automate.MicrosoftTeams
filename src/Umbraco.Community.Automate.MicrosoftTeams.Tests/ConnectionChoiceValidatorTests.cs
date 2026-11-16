using Umbraco.Community.Automate.MicrosoftTeams.Actions;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class ConnectionChoiceValidatorTests
{
    [Fact]
    public void TwoTeamsConnections_NoConnectionId_Blocks() =>
        Assert.Equal(["Choose which Teams connection this step posts to."], ConnectionChoiceValidator.Validate(null, 2));

    [Fact]
    public void TwoTeamsConnections_ConnectionIdSet_Allows() => Assert.Empty(ConnectionChoiceValidator.Validate(Guid.NewGuid(), 2));

    [Fact]
    public void OneTeamsConnection_NoConnectionId_Allows() => Assert.Empty(ConnectionChoiceValidator.Validate(null, 1));

    [Fact]
    public void NoTeamsConnection_NoConnectionId_Allows() => Assert.Empty(ConnectionChoiceValidator.Validate(null, 0));
}
