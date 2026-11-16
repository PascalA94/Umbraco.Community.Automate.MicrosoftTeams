using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Testing;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Connection;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>Runs either Teams action against a <see cref="SenderRig"/>, advancing its fake clock so waits and timeouts finish.</summary>
internal static class CardActionRig
{
    public static ActionTestHarness<TAction> Harness<TAction, TSettings>(SenderRig rig, TSettings settings, string? credential = "sentinel", string? appUrl = "https://www.example.com")
        where TAction : class, IAction
        where TSettings : class
    {
        var routing = Substitute.For<IOptionsMonitor<WebRoutingSettings>>();
        routing.CurrentValue.Returns(new WebRoutingSettings { UmbracoApplicationUrl = appUrl });
        var domains = Substitute.For<IDomainService>();
        domains.GetAllAsync(false).Returns(Task.FromResult<IEnumerable<IDomain>>([]));

        return ActionTestHarness.For<TAction>()
            .WithService<IEditableModelResolver>(new EditableModelResolverStub())
            .WithService<ITeamsWebhookSender>(rig.Sender)
            .WithService<IOptionsMonitor<Configuration.TeamsOptions>>(rig.Monitor)
            .WithService(new LinkHostPolicy(new SiteHostProvider(routing, domains, TimeProvider.System), rig.Monitor))
            .WithService<ICardRenderer>(new CardRenderer())
            .WithService(new CardTemplates())
            .WithSettings(settings)
            .WithConnection(new TeamsWorkflowConnectionSettings { CredentialName = credential });
    }

    public static async Task<ActionResult> Run<TAction>(SenderRig rig, ActionTestHarness<TAction> harness)
        where TAction : class, IAction
    {
        var task = harness.ExecuteAsync();
        for (var i = 0; i < 400 && !task.IsCompleted; i++)
        {
            await Task.Yield();
            rig.Clock.Advance(TimeSpan.FromMilliseconds(50));
        }

        return await task;
    }
}
