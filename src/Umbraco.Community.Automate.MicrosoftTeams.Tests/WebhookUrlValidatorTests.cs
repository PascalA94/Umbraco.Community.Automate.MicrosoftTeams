using CredentialHelperContract;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class WebhookUrlValidatorTests
{
    private static WebhookUrlValidator Sut(bool strict = false)
    {
        var options = new TeamsOptions { StrictHosts = strict };
        new TeamsOptionsPostConfigure().PostConfigure(null, options);
        return new WebhookUrlValidator(new TestOptionsMonitor<TeamsOptions>(options));
    }

    [Theory]
    [InlineData(null, "Webhook URL is required.")]
    [InlineData("  ", "Webhook URL is required.")]
    [InlineData("not a url", "This isn't a valid URL.")]
    [InlineData("/relative/path", "This isn't a valid URL.")]
    [InlineData("http://x.environment.api.powerplatform.com/a?sig=S", "Teams webhook URLs must use https.")]
    public void Check_BlockRows(string? url, string error)
    {
        var c = Sut().Check(url);
        Assert.True(c.IsBlocked);
        Assert.Equal(error, c.Error);
    }

    [Theory]
    [InlineData("https://acme.webhook.office.com/webhookb2/x?sig=S")]
    [InlineData("https://ACME.WEBHOOK.OFFICE.COM/x")]
    [InlineData("https://acme.webhook.office.com./x")]
    [InlineData("https://acme.webhook.office.com:443/x")]
    [InlineData("https://user:pass@acme.webhook.office.com/x")]
    [InlineData("https://xn--acme-9oa.webhook.office.com/x")]
    public void Check_RetiredHostVariants_AreBlocked(string url)
    {
        var c = Sut().Check(url);
        Assert.True(c.IsBlocked);
        Assert.StartsWith("This is a retired Office 365 connector URL. Create a Workflows webhook instead.", c.Error);
        Assert.Contains(TeamsDocs.SetupUrl, c.Error);
    }

    [Fact]
    public void Check_UnknownHost_WarnsButAllows_RunTimeWarningHasNoHost()
    {
        var c = Sut().Check("https://hooks.example.org/p?sig=S");
        Assert.False(c.IsBlocked);
        Assert.Equal("The webhook host isn't a known Power Automate host.", c.Warning);
        Assert.Equal("hooks.example.org isn't a known Power Automate host.", c.TestWarning);
    }

    [Fact]
    public void Check_UnknownHost_StrictHosts_Blocks()
    {
        var c = Sut(strict: true).Check("https://hooks.example.org/p?sig=S");
        Assert.True(c.IsBlocked);
        Assert.Equal("The webhook host isn't in AllowedHosts.", c.Error);
    }

    [Fact]
    public void Check_SentinelBuiltHost_NeverEchoed()
    {
        var c = Sut().Check("https://SENTINELSIG.example.org/p");
        Assert.DoesNotContain("SENTINEL", c.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://under_score.example.org/x")]
    public void Check_NonDnsHost_TestWarningUsesGenericText(string url)
    {
        var c = Sut().Check(url);
        if (!c.IsBlocked)
        {
            Assert.Equal("The webhook host isn't a known Power Automate host.", c.TestWarning);
        }
    }

    [Fact]
    public void Check_OverlongHost_TestWarningUsesGenericText()
    {
        var host = string.Join('.', Enumerable.Repeat(new string('a', 60), 5)) + ".example.org";
        var c = Sut().Check($"https://{host}/x");
        Assert.False(c.IsBlocked);
        Assert.Equal("The webhook host isn't a known Power Automate host.", c.TestWarning);
    }

    [Theory]
    [InlineData("https://powerplatform.com/x")]
    public void Check_WildcardDoesNotMatchApex(string url) => Assert.NotNull(Sut().Check(url).Warning);

    [Theory]
    [InlineData("https://acme.webhoοk.office.com/x")]
    public void Check_IdnHosts_NormalisedBeforeMatching(string url)
    {
        var c = Sut().Check(url);
        Assert.False(c.IsBlocked);
        Assert.NotNull(c.Warning);
        Assert.Contains("xn--", c.TestWarning);
    }

    [Fact]
    public void Check_KnownHost_NoWarning() => Assert.Null(Sut().Check(TestUrls.Sentinel).Warning);

    [Fact]
    public void Check_Messages_NeverContainUserInfoPathOrQuery()
    {
        var urls = new[]
        {
            "https://user:pass@acme.webhook.office.com/SENTINELPATH?sig=SENTINELSIG",
            "https://user:pass@hooks.example.org/SENTINELPATH?sig=SENTINELSIG",
            "http://user:pass@hooks.example.org/SENTINELPATH?sig=SENTINELSIG",
        };
        foreach (var strict in new[] { false, true })
        {
            foreach (var url in urls)
            {
                var c = Sut(strict).Check(url);
                foreach (var text in new[] { c.Error, c.Warning, c.TestWarning })
                {
                    Assert.DoesNotContain("pass", text ?? string.Empty);
                    Assert.DoesNotContain("SENTINEL", text ?? string.Empty);
                    Assert.DoesNotContain("sig=", text ?? string.Empty);
                }
            }
        }
    }

    [Fact]
    public void ValidatedWebhookUrl_ToString_HasNoHostPathOrQuery()
    {
        var url = Sut().Check(TestUrls.Sentinel).Url!;
        Assert.Equal("[webhook URL]", url.ToString());
        Assert.DoesNotContain("powerplatform", $"{url}");
        Assert.DoesNotContain("SENTINEL", $"{url}");
    }
}
