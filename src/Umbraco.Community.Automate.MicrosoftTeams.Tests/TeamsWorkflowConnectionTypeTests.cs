using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Automate.Core.Connections;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Connection;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class TeamsWorkflowConnectionTypeTests
{
    private const string Guard = "Refused: Umbraco:Automate:AllowedConfigurationKeyPrefixes contains 'Umbraco:Automate', which exposes Umbraco:Automate:MicrosoftTeams:Credentials. Remove that entry.";

    private static (TeamsWorkflowConnectionType Type, SenderRig Rig) Make(
        string? url = TestUrls.Sentinel,
        string? appUrl = "https://www.example.com",
        Action<TeamsOptions>? configure = null,
        string[]? extraPrefixes = null,
        RecordingHandler? handler = null,
        IConfiguration? configuration = null,
        ITeamsWebhookSender? senderOverride = null)
    {
        var rig = new SenderRig(url, configure: configure, extraAllowedPrefixes: extraPrefixes, handler: handler, configuration: configuration);
        var routing = Substitute.For<IOptionsMonitor<WebRoutingSettings>>();
        routing.CurrentValue.Returns(new WebRoutingSettings { UmbracoApplicationUrl = appUrl });
        var domains = Substitute.For<IDomainService>();
        domains.GetAllAsync(false).Returns(Task.FromResult<IEnumerable<IDomain>>([]));

        var services = new ServiceCollection();
        services.AddSingleton(senderOverride ?? (ITeamsWebhookSender)rig.Sender);
        services.AddSingleton(routing);
        services.AddSingleton(rig.Store);
        services.AddSingleton(new TeamsCredentialSourceCheck(configuration ?? new ConfigurationBuilder().Build(), rig.Store));
        services.AddSingleton(new SiteHostProvider(routing, domains, TimeProvider.System));
        var type = new TeamsWorkflowConnectionType(new ConnectionTypeInfrastructure(new EditableModelResolverStub()), services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
        return (type, rig);
    }

    private static TeamsWorkflowConnectionSettings Settings(string? name = "sentinel") => new() { CredentialName = name };

    private static string Text(ConnectionValidationResult r) => r.Message ?? string.Empty;

    [Theory]
    [InlineData("https://x.example/a?sig=SENTINELSIG")] // gitleaks:allow
    [InlineData("alerts-SENTINELPATH-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task Validate_UrlPastedAsCredentialName_FixedMessage_NoRequest_NoEcho(string name)
    {
        var (type, rig) = Make();
        var r = await type.ValidateAsync(Settings(name), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Equal("This looks like a webhook URL. It may have been saved on the connection: clear the field and rotate the Workflow URL (see SECURITY.md#rotation).", Text(r));
        Assert.Empty(rig.Handler.Requests);
        Assert.False(TestUrls.Leaks(Text(r)));
        Assert.False(TestUrls.Leaks(r.Details?.ToString()));
        Assert.All(rig.Logs.AllText, t => Assert.False(TestUrls.Leaks(t), t));
    }

    [Fact]
    public async Task Validate_Accepted_PostsTestCard_AndSaysConnected()
    {
        var (type, rig) = Make();
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Success, r.Status);
        Assert.StartsWith("Connected (Teams accepted the message).", Text(r));
        Assert.Contains("Anyone who can build automations in a workspace that allows this connection can post to this channel.", Text(r));
        Assert.Contains("Connection test from www.example.com", Assert.Single(rig.Handler.Requests).Body);
    }

    [Fact]
    public async Task Validate_NoApplicationUrl_UsesGenericSiteName()
    {
        var (type, rig) = Make(appUrl: null);
        await type.ValidateAsync(Settings(), default);
        Assert.Contains("Connection test from your Umbraco site", Assert.Single(rig.Handler.Requests).Body);
    }

    [Fact]
    public async Task Validate_UnknownHost_ConnectedPlusWarning()
    {
        var (type, _) = Make("https://hooks.example.org/p?sig=S");
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Contains("Connected, but hooks.example.org isn't a known Power Automate host. Check the URL.", Text(r));
    }

    [Fact]
    public async Task Validate_UnknownNonDnsHost_GenericHostText()
    {
        var host = string.Join('.', Enumerable.Repeat(new string('a', 60), 5)) + ".example.org";
        var (type, _) = Make($"https://{host}/p");
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Contains("Connected, but the webhook host isn't a known Power Automate host. Check the URL.", Text(r));
        Assert.DoesNotContain(host, Text(r));
    }

    [Fact]
    public async Task Validate_StrictHosts_UnknownHost_Failure_NoRequest()
    {
        var (type, rig) = Make("https://hooks.example.org/p?sig=S", configure: o => o.StrictHosts = true);
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Validate_MissingCredential_Failure_NoRequest_NamesKeyOnly()
    {
        var (type, rig) = Make();
        var r = await type.ValidateAsync(Settings("sentinel2"), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Equal("No value is configured at Umbraco:Automate:MicrosoftTeams:Credentials:sentinel2:WebhookUrl.", Text(r));
        Assert.Empty(rig.Handler.Requests);
    }

    [Theory]
    [InlineData("$Umbraco:Automate:Secrets:X")]
    [InlineData("ops-alerts")]
    [InlineData("alerts\u200B")]
    public async Task Validate_BadCredentialName_Failure_NoRequest_NoEcho(string name)
    {
        var (type, rig) = Make();
        var r = await type.ValidateAsync(Settings(name), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Equal("Credential name must be 1 to 32 letters or digits.", Text(r));
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Validate_GuardTripped_Failure_NoRequest()
    {
        var (type, rig) = Make(extraPrefixes: ["Umbraco:Automate"]);
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Equal(Guard, Text(r));
        Assert.Empty(rig.Handler.Requests);
    }

    [Theory]
    [InlineData("https://acme.webhook.office.com/webhookb2/x")]
    [InlineData("http://x.example/a")]
    public async Task Validate_RetiredOrHttpUrl_Failure_NoRequest(string url)
    {
        var (type, rig) = Make(url);
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Empty(rig.Handler.Requests);
    }

    [Fact]
    public async Task Validate_404_FailureWithMappedMessage()
    {
        var (type, _) = Make(handler: new RecordingHandler { Status = System.Net.HttpStatusCode.NotFound });
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Equal("The workflow was deleted or turned off.", Text(r));
    }

    [Fact]
    public async Task Validate_CredentialFromAppsettingsJson_AddsMoveItWarning()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { Umbraco = new { Automate = new { MicrosoftTeams = new { Credentials = new { sentinel = new { WebhookUrl = TestUrls.Sentinel } } } } } });
        var (type, _) = Make(configuration: JsonFileConfig.Build("appsettings.Development.json", json));
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Success, r.Status);
        Assert.Contains("Warning: Credential 'sentinel' is read from appsettings.Development.json. Move it to environment variables, Umbraco Cloud secrets or Key Vault.", Text(r));
        Assert.False(TestUrls.Leaks(Text(r)));
    }

    [Fact]
    public async Task Validate_NoSiteHosts_AddsButtonsDroppedWarning()
    {
        var (type, _) = Make(appUrl: null);
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Contains("no site domain is configured, so card buttons are dropped", Text(r));
    }

    [Fact]
    public async Task Validate_HandlerThrowsWithSentinelInMessage_ReturnsOurText()
    {
        var sender = Substitute.For<ITeamsWebhookSender>();
        sender.PostAsync(Arg.Any<string?>(), Arg.Any<System.Text.Json.Nodes.JsonObject>(), Arg.Any<CancellationToken>())
            .Returns<Task<TeamsPostResult>>(_ => throw new InvalidOperationException(TestUrls.Sentinel));
        var (type, _) = Make(senderOverride: sender);
        var r = await type.ValidateAsync(Settings(), default);
        Assert.Equal(ConnectionValidationStatus.Failure, r.Status);
        Assert.Equal("Couldn't test the connection.", Text(r));
    }

    [Fact]
    public async Task Validate_CallerCancelled_Throws()
    {
        var (type, _) = Make(handler: new RecordingHandler { Hang = true });
        using var cts = new CancellationTokenSource();
        var task = type.ValidateAsync(Settings(), cts.Token);
        await Task.Yield();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task Validate_NoResultText_ContainsUrl()
    {
        foreach (var handler in new[] { new RecordingHandler(), new RecordingHandler { Status = System.Net.HttpStatusCode.BadRequest, Body = TestUrls.Sentinel } })
        {
            var (type, _) = Make(handler: handler);
            var r = await type.ValidateAsync(Settings(), default);
            Assert.False(TestUrls.Leaks(Text(r)));
            Assert.False(TestUrls.Leaks(r.Details?.ToString()));
        }
    }

    [Fact]
    public async Task Validate_WrongSettingsType_Failure()
    {
        var (type, _) = Make();
        Assert.Equal(ConnectionValidationStatus.Failure, (await type.ValidateAsync(new object(), default)).Status);
    }
}
