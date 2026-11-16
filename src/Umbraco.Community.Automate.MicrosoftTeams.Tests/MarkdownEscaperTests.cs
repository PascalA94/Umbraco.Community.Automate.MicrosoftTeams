using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class MarkdownEscaperTests
{
    private const string Z = "​";

    [Theory]
    [InlineData("[Click me](https://evil.example)", "\\[Click me\\]\\(https:" + Z + "//evil.example\\)")]
    [InlineData(@"a\b*c_d#e>f-g`h", @"a\\b\*c\_d\#e\>f\-g\`h")]
    public void Escape_EscapesSpecCharacters(string input, string expected) => Assert.Equal(expected, MarkdownEscaper.Escape(input));

    [Theory]
    [InlineData("see https://evil.example/x", "see https:" + Z + "//evil.example/x")]
    [InlineData("www.evil.example", "www." + Z + "evil.example")]
    [InlineData("a@evil.example", "a" + Z + "@evil.example")]
    [InlineData("mailto:a@evil.example", "mailto:a" + Z + "@evil.example")]
    public void Escape_BreaksAutoLinks(string input, string expected) => Assert.Equal(expected, MarkdownEscaper.Escape(input));

    [Fact]
    public void Escape_Null_IsEmpty() => Assert.Equal(string.Empty, MarkdownEscaper.Escape(null));

    [Fact]
    public void Escape_SnapshotOfInjectionSet()
    {
        var input = "https://evil.example/x, www.evil.example, a@evil.example, [x](https://evil.example), <at>x</at>, **bold**";
        var expected = "https:" + Z + "//evil.example/x, www." + Z + "evil.example, a" + Z + "@evil.example, \\[x\\]\\(https:" + Z + "//evil.example\\), <at\\>x</at\\>, \\*\\*bold\\*\\*";
        Assert.Equal(expected, MarkdownEscaper.Escape(input));
    }

    [Fact]
    public void Escape_LoneAtSign_NotTreatedAsEmail() => Assert.Equal("@here and @x", MarkdownEscaper.Escape("@here and @x"));
}
