using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class PostCardValidationTests
{
    private const string ValidCard = """{"type":"AdaptiveCard","version":"1.4","body":[{"type":"TextBlock","text":"{{unknown}}"}]}""";

    private static PostCardAction Action(IServiceProvider? services = null) =>
        new(new ActionInfrastructure(new EditableModelResolverStub()), (services ?? new ServiceCollection().BuildServiceProvider()).GetRequiredService<IServiceScopeFactory>());

    private static CardDataRow Row(string key, string? value = "v") => new() { Key = key, Value = value };

    private static Task<IReadOnlyList<string>> Save(PostCardSettings settings) => Action().ValidateSettingsAsync(settings, default);

    // With no services given, the workspace has no Teams connection, so the connection rule has nothing to say.
    private static Task<IReadOnlyList<string>> Publish(PostCardSettings settings, Automation? automation = null, IServiceProvider? services = null)
    {
        var (defaultServices, defaultAutomation) = Workspace(0, null);
        return Action(services ?? defaultServices).ValidateSettingsForPublishAsync(settings, automation ?? defaultAutomation, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_CustomBlankJson_Error(string json) =>
        Assert.Equal(["Custom card JSON is required."], await Save(new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = json }));

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{"type":"AdaptiveCard",}""")]
    public async Task Save_CustomInvalidJson_Error(string json)
    {
        var errors = await Save(new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = json });
        Assert.StartsWith("Custom card JSON isn't valid JSON", Assert.Single(errors));
    }

    [Theory]
    [InlineData("""{"type":"Foo"}""")]
    [InlineData("[]")]
    [InlineData("42")]
    [InlineData("""{"version":"1.4"}""")]
    [InlineData("""{"type":"message"}""")]
    [InlineData("""{"type":"message","attachments":"x"}""")]
    public async Task Save_CustomRootNotCardOrEnvelope_Error(string json) =>
        Assert.Equal(["Custom card JSON must be an AdaptiveCard, or a message with an attachments array."], await Save(new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = json }));

    [Theory]
    [InlineData(ValidCard)]
    [InlineData("""{"type":"message","attachments":[{"contentType":"application/vnd.microsoft.card.adaptive","content":{"type":"AdaptiveCard","version":"1.4","body":[]}}]}""")]
    public async Task Save_CustomValidRoot_NoError(string json) =>
        Assert.Empty(await Save(new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = json }));

    [Fact]
    public async Task Save_DuplicateKeysCaseInsensitive_Error()
    {
        var errors = await Save(new PostCardSettings { CardData = [Row("title"), Row(" Title ")] });
        Assert.Equal(["Card data has the key 'title' more than once."], errors);
    }

    [Fact]
    public async Task Save_CustomUnknownPlaceholder_NoError() =>
        Assert.Empty(await Save(new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = ValidCard }));

    [Fact]
    public async Task Save_BuiltIn_NoErrors() =>
        Assert.Empty(await Save(new PostCardSettings { CardSource = CardSource.Simple, CustomCardJson = "{ ignored because the field is hidden" }));

    [Fact]
    public async Task Save_NullSettings_NoErrors() => Assert.Empty(await Action().ValidateSettingsAsync(null, default));

    [Fact]
    public async Task Publish_ApprovalNeededWithoutUrl_Error() =>
        Assert.Equal(["Approval needed card needs a url value"], await Publish(new PostCardSettings { CardSource = CardSource.ApprovalNeeded, CardData = [Row("title")] }));

    public static TheoryData<CardSource, string> RequiredKeyCases => new()
    {
        { CardSource.ContentPublished, "title" }, { CardSource.ContentPublished, "url" },
        { CardSource.ApprovalNeeded, "title" }, { CardSource.ApprovalNeeded, "url" },
        { CardSource.FormSubmitted, "formName" }, { CardSource.FormSubmitted, "url" },
        { CardSource.Simple, "title" }, { CardSource.Simple, "text" },
    };

    [Theory]
    [MemberData(nameof(RequiredKeyCases))]
    public async Task Publish_MissingRequiredKey_Error(CardSource source, string missing)
    {
        var templates = new CardTemplates();
        var rows = templates.RequiredKeys(source).Where(k => k != missing).Select(k => Row(k)).ToList();
        var errors = await Publish(new PostCardSettings { CardSource = source, CardData = rows });
        Assert.Equal([$"{templates.DisplayName(source)} card needs a {missing} value"], errors);
    }

    [Fact]
    public async Task Publish_RequiredKeyWithBlankValue_CountsAsMissing() =>
        Assert.Equal(["Simple card needs a text value"], await Publish(new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title"), Row("TEXT", "  ")] }));

    [Fact]
    public async Task Publish_RequiredKeyMatchIsCaseInsensitive_NoError() =>
        Assert.Empty(await Publish(new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("TITLE"), Row("Text", "${trigger.body}")] }));

    [Fact]
    public async Task Publish_Custom_NeedsNoKeys() =>
        Assert.Empty(await Publish(new PostCardSettings { CardSource = CardSource.Custom, CustomCardJson = ValidCard }));

    private static (IServiceProvider Services, Automation Automation) Workspace(int teamsConnections, Guid? stepConnection)
    {
        var workspaceId = Guid.NewGuid();
        var ids = Enumerable.Range(0, teamsConnections + 1).Select(_ => Guid.NewGuid()).ToList(); // plus one connection of another type
        var workspaces = Substitute.For<IWorkspaceService>();
        workspaces.GetWorkspaceAsync(workspaceId, Arg.Any<CancellationToken>()).Returns(new Workspace { Id = workspaceId, Alias = "w", Name = "w", AllowedConnections = ids });
        var connections = Substitute.For<IConnectionService>();
        for (var i = 0; i < ids.Count; i++)
        {
            var type = i < teamsConnections ? ConnectionChoiceValidator.ConnectionTypeAlias : "community.other";
            connections.GetConnectionAsync(ids[i], Arg.Any<CancellationToken>()).Returns(new Umbraco.Automate.Core.Connections.Connection { Id = ids[i], Alias = "c" + i, Name = "c" + i, Type = type });
        }

        var services = new ServiceCollection().AddSingleton(workspaces).AddSingleton(connections).BuildServiceProvider();
        var automation = new Automation
        {
            Alias = "a",
            Name = "a",
            WorkspaceId = workspaceId,
            Steps = [new StepConfiguration { Id = Guid.NewGuid(), Alias = "s", Name = "s", ActionAlias = "community.teams.postCard", ConnectionId = stepConnection }],
        };
        return (services, automation);
    }

    private static PostCardSettings Complete() => new() { CardSource = CardSource.Simple, CardData = [Row("title"), Row("text")] };

    [Fact]
    public async Task Publish_TwoTeamsConnections_NoConnectionId_Error()
    {
        var (services, automation) = Workspace(2, null);
        Assert.Equal(["Choose which Teams connection this step posts to."], await Publish(Complete(), automation, services));
    }

    [Fact]
    public async Task Publish_TwoTeamsConnections_ConnectionChosen_NoError()
    {
        var (services, automation) = Workspace(2, Guid.NewGuid());
        Assert.Empty(await Publish(Complete(), automation, services));
    }

    [Fact]
    public async Task Publish_OneTeamsConnection_NoConnectionId_NoError()
    {
        var (services, automation) = Workspace(1, null);
        Assert.Empty(await Publish(Complete(), automation, services));
    }

    [Fact]
    public async Task Publish_CardErrorAndConnectionError_BothReported()
    {
        var (services, automation) = Workspace(2, null);
        var errors = await Publish(new PostCardSettings { CardSource = CardSource.Simple, CardData = [Row("title")] }, automation, services);
        Assert.Equal(["Simple card needs a text value", "Choose which Teams connection this step posts to."], errors);
    }
}
