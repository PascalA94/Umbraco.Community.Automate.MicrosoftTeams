namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>Fake webhook URLs for secret tests. A leak is any captured text holding SENTINELPATH, SENTINELSIG or SENTINELHOST (any case, as hosts are lower-cased).</summary>
internal static class TestUrls
{
    // gitleaks:allow
    public const string Sentinel = "https://SENTINELHOST0123.ab.environment.api.powerplatform.com/powerautomate/automations/direct/workflows/SENTINELPATH/triggers/manual/paths/invoke?api-version=1&sig=SENTINELSIG";

    public static bool Leaks(string? text) =>
        text is not null
        && (text.Contains("SENTINELPATH", StringComparison.Ordinal)
            || text.Contains("SENTINELSIG", StringComparison.Ordinal)
            || text.Contains("SENTINELHOST", StringComparison.OrdinalIgnoreCase));
}
