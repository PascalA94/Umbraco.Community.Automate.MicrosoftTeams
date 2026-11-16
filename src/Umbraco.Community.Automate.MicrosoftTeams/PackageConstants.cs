namespace Umbraco.Community.Automate.MicrosoftTeams;

/// <summary>
/// Names that are permanent once the package is released: the NuGet ID and the Automate aliases (the package ships no client assets, so there is no App_Plugins folder). Changing one breaks installed sites.
/// </summary>
public static class PackageConstants
{
    public const string PackageName = "Umbraco.Community.Automate.MicrosoftTeams";

    /// <summary>Folder under /App_Plugins/ that serves the built client from wwwroot.</summary>
    public const string AppPluginsFolder = "MicrosoftTeams";

    /// <summary>Prefix for every backoffice extension alias this package registers.</summary>
    public const string AliasPrefix = "MicrosoftTeams";
}
