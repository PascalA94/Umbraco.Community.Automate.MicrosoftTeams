using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Umbraco.Community.Automate.MicrosoftTeams.Credentials;

/// <summary>
/// Tells the connection test whether the credential is supplied by an appsettings file, which is usually committed to
/// source control. It returns a file name only: the value is read to see that it exists and then dropped.
/// </summary>
// Contract C28 and C31 pin this type: it is handed configuration and calls KeyFor, for the file-name check only.
internal sealed partial class TeamsCredentialSourceCheck(IConfiguration configuration, TeamsCredentialStore store)
{
    public string? AppsettingsFileSupplying(string normalisedName)
    {
        if (configuration is not IConfigurationRoot root)
        {
            return null;
        }

        var key = store.KeyFor(normalisedName, TeamsCredentialPart.WebhookUrl);

        // The last provider that has the key wins, so walk in reverse and stop at the first.
        foreach (var provider in root.Providers.Reverse())
        {
            if (!provider.TryGet(key, out _))
            {
                continue;
            }

            if (provider is JsonConfigurationProvider json)
            {
                var file = Path.GetFileName(json.Source.Path);
                if (file is not null && AppsettingsFile().IsMatch(file))
                {
                    return file;
                }
            }

            return null;
        }

        return null;
    }

    [GeneratedRegex(@"^appsettings(\..+)?\.json\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AppsettingsFile();
}
