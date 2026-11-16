using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Credentials;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Web;

/// <summary>The package's only composer. Register services, handlers and options here.</summary>
public sealed class MicrosoftTeamsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // The options section is a parent of the credential section. TeamsOptions has no member that could take a
        // child, so the credentials never bind into it (contract C31).
        builder.Services.AddOptions<TeamsOptions>().Bind(builder.Config.GetSection(TeamsOptions.SectionName));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<TeamsOptions>, TeamsOptionsPostConfigure>());
        builder.Services.AddSingleton<WebhookUrlValidator>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SiteHostProvider>();
        builder.Services.AddSingleton<LinkHostPolicy>();
        builder.Services.AddSingleton<ICardRenderer, CardRenderer>();
        builder.Services.AddSingleton<CardTemplates>();
        builder.Services.AddSingleton<WebhookRateLimiter>();
        builder.Services.AddSingleton<ITeamsWebhookSender, TeamsWebhookSender>();

        // Contract 1.1: the store, its startup check and nothing else touches the credential section.
        builder.Services.AddSingleton<TeamsCredentialStore>();
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, TeamsCredentialStoreStartupCheck>();
        builder.Services.AddSingleton<TeamsCredentialSourceCheck>();
    }
}
