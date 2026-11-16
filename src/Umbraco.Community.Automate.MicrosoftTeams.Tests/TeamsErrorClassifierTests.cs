using Umbraco.Automate.Core.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Http;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class TeamsErrorClassifierTests
{
    [Theory]
    [InlineData(400, StepRunErrorCategory.InvalidResponse, "Teams rejected the card (400).")]
    [InlineData(401, StepRunErrorCategory.Authentication, "Teams refused the webhook signature. Re-create the Workflow URL.")]
    [InlineData(403, StepRunErrorCategory.Authentication, "Teams refused the webhook signature. Re-create the Workflow URL.")]
    [InlineData(404, StepRunErrorCategory.ConfigurationError, "The workflow was deleted or turned off.")]
    [InlineData(413, StepRunErrorCategory.Validation, "Teams says the message is too large.")]
    [InlineData(429, StepRunErrorCategory.RateLimiting, "Teams is throttling this webhook. Use the step's Retry setting.")]
    [InlineData(500, StepRunErrorCategory.ServiceUnavailable, "Teams returned 500. Try again later.")]
    [InlineData(503, StepRunErrorCategory.ServiceUnavailable, "Teams returned 503. Try again later.")]
    [InlineData(409, StepRunErrorCategory.InvalidResponse, "Teams returned 409.")]
    [InlineData(307, StepRunErrorCategory.InvalidResponse, "Teams answered with a redirect. Check the webhook URL.")]
    public void Classify_MapsStatus(int code, StepRunErrorCategory cat, string msg)
    {
        var f = TeamsErrorClassifier.Classify(code, null);
        Assert.NotNull(f);
        Assert.Equal(cat, f.Category);
        Assert.Equal(msg, f.Message);
    }

    [Fact]
    public void Classify_400WithSnippet_AppendsScrubbedText() =>
        Assert.Equal("Teams rejected the card (400). bad card", TeamsErrorClassifier.Classify(400, "bad card")!.Message);

    [Theory]
    [InlineData(200)]
    [InlineData(202)]
    [InlineData(204)]
    public void Classify_2xx_IsNull(int code) => Assert.Null(TeamsErrorClassifier.Classify(code, null));
}
