using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class PersonalDataKeysTests
{
    [Theory]
    [InlineData("email")]
    [InlineData("contactEmail")]
    [InlineData("phone_number")]
    [InlineData("fullName")]
    [InlineData("Name")]
    [InlineData("postcode")]
    public void Warns(string key) =>
        Assert.Equal($"Card key '{key}' may hold personal data. See the GDPR notes.", Assert.Single(PersonalDataKeys.Warnings([key])));

    [Theory]
    [InlineData("formName")]
    [InlineData("entryId")]
    [InlineData("editor")]
    [InlineData("title")]
    public void DoesNotWarn(string key) => Assert.Empty(PersonalDataKeys.Warnings([key]));
}
