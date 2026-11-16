// credential-helper-contract: 1.0.2
global using Umbraco.Community.Automate.MicrosoftTeams.Credentials;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Cms.Core.Notifications;
using Xunit.Abstractions;

namespace CredentialHelperContract;

// The only test file a consumer package writes (contract section 5.3). The global using supplies the
// namespace of the package's store to the verbatim suites.
public sealed class TeamsCredentialStoreContractTests : CredentialStoreContractTests<TeamsCredentialPart>
{
    protected override string Prefix => TeamsCredentialStore.Prefix;

    protected override string Namespace => "Umbraco.Community.Automate.MicrosoftTeams.Credentials";

    protected override string StoreClassName => "TeamsCredentialStore";

    protected override string PartEnumName => "TeamsCredentialPart";

    // C31: every package type outside the store file that is handed configuration, fetches it, or names a parent of the
    // credential section.
    protected override IReadOnlyDictionary<string, string> PinnedConfigurationReaders { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Umbraco.Community.Automate.MicrosoftTeams.Web.MicrosoftTeamsComposer"] = "binds TeamsOptions from the parent section Umbraco:Automate:MicrosoftTeams; TeamsOptions has no catch-all member",
        ["Umbraco.Community.Automate.MicrosoftTeams.Credentials.TeamsCredentialSourceCheck"] = "reads provider presence to name an appsettings file; never returns or logs a value",
    };

    // The options type is bound from Umbraco:Automate:MicrosoftTeams, a parent of the credential section.
    protected override IReadOnlyList<Type> ParentSectionBoundTypes { get; } = [typeof(Umbraco.Community.Automate.MicrosoftTeams.Configuration.TeamsOptions)];

    // C28: types that call KeyFor outside the store file.
    protected override IReadOnlyDictionary<string, string> PinnedKeyForCallers { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Umbraco.Community.Automate.MicrosoftTeams.Credentials.TeamsCredentialSourceCheck"] = "message-only: finds which provider holds the key; the value is discarded",
    };

    internal override ICredentialStore<TeamsCredentialPart> CreateStore(IConfiguration configuration, IOptionsMonitor<AutomateOptions> options, IOptions<AutomateOptions> startupOptions) =>
        new TeamsCredentialStore(configuration, options, startupOptions);

    internal override object CreateStartupCheck(ICredentialStore<TeamsCredentialPart> store, ILoggerFactory loggerFactory) =>
        new TeamsCredentialStoreStartupCheck((TeamsCredentialStore)store, loggerFactory.CreateLogger<TeamsCredentialStoreStartupCheck>());

    internal override Task RunStartupCheck(object check)
    {
        ((TeamsCredentialStoreStartupCheck)check).Handle(new UmbracoApplicationStartedNotification(false));
        return Task.CompletedTask;
    }
}

public sealed class TeamsCredentialStoreIsolationTests(ITestOutputHelper output) : CredentialIsolationContractTests<TeamsCredentialPart>(output)
{
    protected override string Prefix => TeamsCredentialStore.Prefix;

    protected override TeamsCredentialPart SamplePart => TeamsCredentialPart.WebhookUrl;

    internal override ICredentialStore<TeamsCredentialPart> CreateStore(IConfiguration configuration, IOptionsMonitor<AutomateOptions> options, IOptions<AutomateOptions> startupOptions) =>
        new TeamsCredentialStore(configuration, options, startupOptions);
}
