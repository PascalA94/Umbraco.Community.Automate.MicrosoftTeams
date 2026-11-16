using System.Text.Json.Nodes;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class MessageCardBuilderTests
{
    private const string Z = "​";
    private static readonly LinkChecker Links = new([("www.example.com", false)], []);

    private static Cards.RenderOutput Build(PostMessageSettings s, Dictionary<string, string>? data = null, string version = "1.4") =>
        MessageCardBuilder.Build(s, data ?? [], version, Links);

    private static JsonObject Card(Cards.RenderOutput o) => (JsonObject)o.Envelope["attachments"]![0]!["content"]!;

    private static JsonArray Items(Cards.RenderOutput o) => Card(o)["body"]![0]!["items"]!.AsArray();

    private static string MessageText(Cards.RenderOutput o) => (string)Items(o).Last(i => (string?)i!["type"] == "TextBlock")!["text"]!;

    [Fact]
    public void Build_TitleAsBoldHeading_MessageAsTextBlock_StyleOnContainer()
    {
        var o = Build(new PostMessageSettings { Title = "Hello", Message = "World", Style = MessageStyle.Good });
        Assert.Equal("good", (string?)Card(o)["body"]![0]!["style"]);
        Assert.Equal("Bolder", (string?)Items(o)[0]!["weight"]);
        Assert.Equal("Hello", (string?)Items(o)[0]!["text"]);
        Assert.Equal("World", (string?)Items(o)[1]!["text"]);
    }

    [Fact]
    public void Build_PlainText_EscapesTitleAndMessage()
    {
        var o = Build(new PostMessageSettings { Message = "[Click](https://evil.example) https://evil.example/login" });
        Assert.Equal("\\[Click\\]\\(https:" + Z + "//evil.example\\) https:" + Z + "//evil.example/login", MessageText(o));
    }

    [Fact]
    public void Build_PlainText_EachCharacterEscapedOnce()
    {
        var o = Build(new PostMessageSettings { Message = "a*b {{k}}" }, new() { ["k"] = "c*d" });
        Assert.Equal(@"a\*b c\*d", MessageText(o));
    }

    [Fact]
    public void Build_PlainText_PlaceholderKeyWithUnderscore_Substituted()
    {
        var o = Build(new PostMessageSettings { Message = "{{form_name}}" }, new() { ["form_name"] = "Contact" });
        Assert.Equal("Contact", MessageText(o));
    }

    [Fact]
    public void Build_Markdown_LeavesAuthorMarkdown()
    {
        var o = Build(new PostMessageSettings { Message = "**bold** text", Formatting = MessageFormatting.Markdown });
        Assert.Equal("**bold** text", MessageText(o));
    }

    [Fact]
    public void Build_Markdown_DataPlaceholder_EscapedOnce()
    {
        var o = Build(new PostMessageSettings { Message = "**Hi** {{name}}", Formatting = MessageFormatting.Markdown }, new() { ["name"] = "[x](https://evil.example)" });
        Assert.Equal("**Hi** \\[x\\]\\(https:" + Z + "//evil.example\\)", MessageText(o));
    }

    [Fact]
    public void Build_BoundTitleContainingPlaceholder_IsSubstituted_Accepted()
    {
        var o = Build(new PostMessageSettings { Title = "{{email}}", Message = "m" }, new() { ["email"] = "a@b.example" });
        Assert.Equal("a" + Z + "@b.example", (string?)Items(o)[0]!["text"]);
    }

    [Fact]
    public void Build_DataContainingPlaceholder_IsNotReSubstituted()
    {
        var o = Build(new PostMessageSettings { Message = "{{a}}" }, new() { ["a"] = "{{b}}", ["b"] = "SECOND" });
        Assert.Equal("{{b}}", MessageText(o));
        Assert.DoesNotContain("SECOND", MessageText(o));
    }

    [Fact]
    public void Build_UnknownDataKey_EmptyAndWarning()
    {
        var o = Build(new PostMessageSettings { Message = "x{{nope}}y" });
        Assert.Equal("xy", MessageText(o));
        Assert.Contains("Unknown card key: nope", o.Warnings);
    }

    [Fact]
    public void Build_DataRows_BecomeEscapedFacts()
    {
        var o = Build(new PostMessageSettings { Message = "m" }, new() { ["form name"] = "a*b" });
        var fact = Items(o).Last()!["facts"]![0]!;
        Assert.Equal("form name", (string?)fact["title"]);
        Assert.Equal(@"a\*b", (string?)fact["value"]);
    }

    [Fact]
    public void Build_LinkUrlOnSiteHost_AddsOpenUrl_DefaultLabelOpen()
    {
        var o = Build(new PostMessageSettings { Message = "m", LinkUrl = "https://www.example.com/news" });
        var action = Card(o)["actions"]![0]!;
        Assert.Equal("Action.OpenUrl", (string?)action["type"]);
        Assert.Equal("Open", (string?)action["title"]);
        Assert.Equal("https://www.example.com/news", (string?)action["url"]);
    }

    [Theory]
    [InlineData("/news/item")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://x")]
    [InlineData("https://evil.example/x")]
    [InlineData("http://evil.example/x")]
    public void Build_BadOrExternalLinkUrl_DropsButton_AddsWarning(string link)
    {
        var o = Build(new PostMessageSettings { Message = "m", LinkUrl = link });
        Assert.Null(Card(o)["actions"]);
        var warning = Assert.Single(o.Warnings);
        Assert.StartsWith("The link button was removed: ", warning);
        Assert.DoesNotContain("evil", warning);
    }

    [Fact]
    public void Build_ExternalLink_AllowExternalLinks_Kept()
    {
        var o = Build(new PostMessageSettings { Message = "m", LinkUrl = "https://partner.example/x", LinkLabel = "Partner page", AllowExternalLinks = true });
        Assert.Equal("Partner page", (string?)Card(o)["actions"]![0]!["title"]);
    }

    [Fact]
    public void Build_SetsCardVersion() => Assert.Equal("1.5", (string?)Card(Build(new PostMessageSettings { Message = "m" }, version: "1.5"))["version"]);

    [Fact]
    public void Settings_DefaultFormatting_IsPlainText() => Assert.Equal(MessageFormatting.PlainText, new PostMessageSettings().Formatting);
}
