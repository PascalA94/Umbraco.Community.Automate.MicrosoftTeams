using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class SecretScrubberTests
{
    [Fact]
    public void Scrub_RedactsUrlsAndSig_AndTruncates()
    {
        var s = SecretScrubber.Scrub($"bad {TestUrls.Sentinel} x sig=ABC&y " + new string('z', 500));
        Assert.DoesNotContain("SENTINEL", s);
        Assert.DoesNotContain("ABC", s);
        Assert.Contains("[redacted]", s);
        Assert.True(s.Length <= 200);
    }

    [Fact]
    public void Scrub_StripsControlAndBidiCharacters()
    {
        var s = SecretScrubber.Scrub("‮abc\u0007⁦d");
        Assert.Equal("abcd", s);
    }

    [Fact]
    public void Scrub_ControlCharacterInsideSig_StillRedacted()
    {
        var s = SecretScrubber.Scrub("si\u0007g=SECRETVALUE");
        Assert.DoesNotContain("SECRETVALUE", s);
    }

    [Fact]
    public void Scrub_NeverSplitsSurrogatePair()
    {
        var s = SecretScrubber.Scrub(new string('a', 199) + "😀", 200);
        Assert.Equal(199, s.Length);
    }

    [Fact]
    public void Scrub_Null_IsEmpty() => Assert.Equal(string.Empty, SecretScrubber.Scrub(null));

    [Fact]
    public void Scrub_JsonEscapedUrl_Redacted()
    {
        var s = SecretScrubber.Scrub("{\"e\":\"https:\\/\\/h.example\\/SECRETPATH?x=1\"}");
        Assert.DoesNotContain("SECRETPATH", s);
    }

    [Theory]
    [InlineData("sig%3DSECRETVALUE")]
    [InlineData("SIG%3dSECRETVALUE")]
    [InlineData("Sig=SECRETVALUE")]
    [InlineData("sig\\u003dSECRETVALUE")]
    public void Scrub_SigVariants_Redacted(string text) => Assert.DoesNotContain("SECRETVALUE", SecretScrubber.Scrub("x " + text + " y"));

    [Fact]
    public void Scrub_BareAllowedHostWithPath_Redacted()
    {
        var s = SecretScrubber.Scrub("bad abc.environment.api.powerplatform.com/p/SECRETPATH ok other.example.org", 200, ["*.powerplatform.com"]);
        Assert.DoesNotContain("powerplatform", s);
        Assert.DoesNotContain("SECRETPATH", s);
        Assert.Contains("other.example.org", s);
    }

    [Fact]
    public void Scrub_ValidatedHost_Redacted()
    {
        var s = SecretScrubber.Scrub("bad hook.example.org:443/SECRETPATH", 200, [], "hook.example.org");
        Assert.DoesNotContain("hook.example.org", s);
        Assert.DoesNotContain("SECRETPATH", s);
    }

    // The host regex backtracks quadratically on a long run of letters and has a 250 ms match timeout. A slow CI runner
    // probably reached it on a 1 KB error body, and the sender's catch-all then reported ServiceUnavailable instead of
    // the real category. 200,000 characters are far over the limit on any machine, so this tests the catch itself.
    // If a future regex engine scans this input in linear time, raise the length or give the scrubber a timeout seam.
    [Fact]
    public void Scrub_RegexTimesOut_ReturnsFixedTextInsteadOfThrowing()
    {
        var s = SecretScrubber.Scrub(new string('x', 200_000), 200, ["*.powerplatform.com"], "hook.example.org");
        Assert.Equal(SecretScrubber.UnreadableBodyText, s);
    }
}
