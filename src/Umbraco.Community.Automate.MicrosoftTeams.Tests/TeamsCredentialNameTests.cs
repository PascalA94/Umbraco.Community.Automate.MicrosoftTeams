namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class TeamsCredentialNameTests
{
    [Theory]
    [InlineData("https://default0123.ab.environment.api.powerplatform.com/x?sig=SENTINELSIG")] // gitleaks:allow
    [InlineData("$Umbraco:Automate:Secrets:X")]
    [InlineData("ops-alerts")]
    [InlineData("Ops_Alerts")]
    public void TryNormaliseName_PastedOrLegacyNames_RejectedWithoutEcho(string name)
    {
        var store = Support.TestCredentials.Store("x", null);
        Assert.False(store.TryNormaliseName(name, out var normalised, out var error));
        Assert.Null(normalised);
        Assert.Equal("Credential name must be 1 to 32 letters or digits.", error);
        Assert.DoesNotContain("SENTINEL", error);
        Assert.DoesNotContain("Secrets", error);
    }

    [Fact]
    public void KeyFor_UsesTeamsPrefixAndPartName() =>
        Assert.Equal("Umbraco:Automate:MicrosoftTeams:Credentials:alerts:WebhookUrl", Support.TestCredentials.Store("x", null).KeyFor("alerts", TeamsCredentialPart.WebhookUrl));
}
