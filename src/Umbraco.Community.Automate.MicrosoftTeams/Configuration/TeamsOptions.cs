namespace Umbraco.Community.Automate.MicrosoftTeams.Configuration;

/// <summary>
/// The <c>Umbraco:Automate:MicrosoftTeams</c> section. Defaults come from <see cref="TeamsWorkflowsFacts"/>; a site can
/// override every one. Credentials live in a child section and are never bound here (plan Task 6 adds validation).
/// </summary>
public sealed class TeamsOptions
{
    /// <summary>The configuration section this class binds.</summary>
    public const string SectionName = "Umbraco:Automate:MicrosoftTeams";

    /// <summary>
    /// Host patterns a webhook URL may use. Empty here on purpose: .NET's binder appends configured items to an array's
    /// default, so the defaults are applied by <see cref="TeamsOptionsPostConfigure"/> only when nothing is configured.
    /// </summary>
    public string[] AllowedHosts { get; set; } = [];

    /// <summary>When true, a host outside <see cref="AllowedHosts"/> blocks the post instead of warning.</summary>
    public bool StrictHosts { get; set; }

    /// <summary>Hosts a card button may link to besides this site's own domains.</summary>
    public string[] AllowedLinkHosts { get; set; } = [];

    /// <summary>The largest payload sent, in UTF-8 bytes.</summary>
    public int MaxPayloadBytes { get; set; } = TeamsWorkflowsFacts.MaxPayloadBytes;

    /// <summary>The longest bound value kept, in characters.</summary>
    public int MaxValueLength { get; set; } = 2000;

    /// <summary>The request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>The card version declared when a card has none.</summary>
    public string DefaultCardVersion { get; set; } = TeamsWorkflowsFacts.DefaultCardVersion;

    /// <summary>The card versions the connection offers. Defaulted by <see cref="TeamsOptionsPostConfigure"/> for the same reason as <see cref="AllowedHosts"/>.</summary>
    public string[] CardVersions { get; set; } = [];

    /// <summary>The per-webhook limiter.</summary>
    public RateLimitOptions RateLimit { get; set; } = new();

    /// <summary>The host patterns used when none are configured.</summary>
    public static IReadOnlyList<string> DefaultAllowedHosts => TeamsWorkflowsFacts.DefaultAllowedHosts;

    /// <summary>Per-webhook token bucket settings.</summary>
    public sealed class RateLimitOptions
    {
        /// <summary>Sustained requests a second.</summary>
        public int RequestsPerSecond { get; set; } = TeamsWorkflowsFacts.RequestsPerSecond;

        /// <summary>Burst size.</summary>
        public int Burst { get; set; } = TeamsWorkflowsFacts.Burst;

        /// <summary>The longest a step waits for a token, in seconds.</summary>
        public int MaxWaitSeconds { get; set; } = TeamsWorkflowsFacts.MaxWaitSeconds;
    }
}

/// <summary>Applies the defaults that the binder would append to, and clamps values that would break the limiter.</summary>
internal sealed class TeamsOptionsPostConfigure : Microsoft.Extensions.Options.IPostConfigureOptions<TeamsOptions>
{
    public void PostConfigure(string? name, TeamsOptions options)
    {
        if (options.AllowedHosts is not { Length: > 0 })
        {
            options.AllowedHosts = [.. TeamsOptions.DefaultAllowedHosts];
        }

        if (options.CardVersions is not { Length: > 0 })
        {
            options.CardVersions = [.. TeamsWorkflowsFacts.CardVersions];
        }

        options.AllowedLinkHosts ??= [];
        // The shared client's own 100 s timeout would otherwise cut in first and report the wrong number.
        options.TimeoutSeconds = Math.Clamp(options.TimeoutSeconds, 1, 100);
        options.MaxValueLength = Math.Max(1, options.MaxValueLength);
        options.RateLimit ??= new TeamsOptions.RateLimitOptions();
        options.RateLimit.Burst = Math.Max(1, options.RateLimit.Burst);
        options.RateLimit.RequestsPerSecond = Math.Max(1, options.RateLimit.RequestsPerSecond);
        options.RateLimit.MaxWaitSeconds = Math.Clamp(options.RateLimit.MaxWaitSeconds, 0, options.TimeoutSeconds);
    }
}
