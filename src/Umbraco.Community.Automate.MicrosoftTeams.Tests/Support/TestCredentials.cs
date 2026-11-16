using CredentialHelperContract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

internal static class TestCredentials
{
    /// <summary>A store over an in-memory configuration holding one credential, plus any extra allow-listed prefixes.</summary>
    public static TeamsCredentialStore Store(string name, string? url, params string[] extraAllowedPrefixes)
    {
        var values = new Dictionary<string, string?>();
        if (url is not null)
        {
            values[$"{TeamsCredentialStore.Prefix}:{name}:WebhookUrl"] = url;
        }

        return Store(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), extraAllowedPrefixes);
    }

    public static TeamsCredentialStore Store(IConfiguration config, params string[] extraAllowedPrefixes)
    {
        var automate = new AutomateOptions();
        automate.AllowedConfigurationKeyPrefixes = [.. automate.AllowedConfigurationKeyPrefixes, .. extraAllowedPrefixes];
        return new TeamsCredentialStore(config, new TestOptionsMonitor<AutomateOptions>(automate), Options.Create(automate));
    }
}
