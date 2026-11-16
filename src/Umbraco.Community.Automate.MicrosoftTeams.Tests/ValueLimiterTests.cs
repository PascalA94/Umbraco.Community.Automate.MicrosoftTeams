using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class ValueLimiterTests
{
    [Fact]
    public void Truncate_UnderLimit_Unchanged() => Assert.Equal(("abc", false), ValueLimiter.Truncate("abc", 5));

    [Fact]
    public void Truncate_OverLimit_EndsWithEllipsis_WithinLimitPlusOne()
    {
        var (v, t) = ValueLimiter.Truncate("abcdefghij", 5);
        Assert.True(t);
        Assert.Equal("abcde…", v);
        Assert.True(v.Length <= 6);
    }

    [Theory]
    [InlineData("a👍🏽b👍🏽c", 4)]
    [InlineData("éééé", 3)]
    public void Truncate_NeverSplitsGrapheme(string input, int max)
    {
        var (v, _) = ValueLimiter.Truncate(input, max);
        var body = v[..^1];
        Assert.True(body.Length <= max);
        Assert.False(body.Length > 0 && char.IsHighSurrogate(body[^1]));
        Assert.False(body.Length > 0 && body[^1] == '́' == false && input.StartsWith(body + "́", StringComparison.Ordinal));
        Assert.StartsWith(body, input, StringComparison.Ordinal);
    }

    [Fact]
    public void Truncate_100KbValue_IsAtMost2000()
    {
        var (v, t) = ValueLimiter.Truncate(new string('x', 100_000), 2000);
        Assert.True(t);
        Assert.True(v.Length <= 2001);
    }

    [Fact]
    public void Truncate_Null_IsEmpty() => Assert.Equal((string.Empty, false), ValueLimiter.Truncate(null, 5));
}
