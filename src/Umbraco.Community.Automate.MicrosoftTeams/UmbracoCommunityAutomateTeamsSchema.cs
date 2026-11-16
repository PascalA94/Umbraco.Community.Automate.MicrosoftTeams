// Schema wrapper consumed by the JsonSchemaGenerate MSBuild task at build time. It describes the appsettings.json shape
// below Umbraco:Automate:MicrosoftTeams so editors get IntelliSense against
// appsettings-schema.Umbraco.Community.Automate.MicrosoftTeams.json. Defaults mirror TeamsOptions and TeamsWorkflowsFacts.
// It is in the global namespace so the MSBuild task can name it by type name alone (as Jira's connector does).
// Never add an example value for a webhook URL here: the generated schema ships in the package.

internal sealed class UmbracoCommunityAutomateTeamsSchema
{
    /// <summary>
    /// Configuration container for all Umbraco products.
    /// </summary>
    public required UmbracoDefinition Umbraco { get; set; }

    public sealed class UmbracoDefinition
    {
        /// <summary>
        /// Configuration of Umbraco Automate.
        /// </summary>
        public required UmbracoAutomateDefinition Automate { get; set; }
    }

    public sealed class UmbracoAutomateDefinition
    {
        /// <summary>
        /// Configuration of the Microsoft Teams add-on.
        /// </summary>
        public required TeamsDefinition MicrosoftTeams { get; set; }
    }

    public sealed class TeamsDefinition
    {
        /// <summary>
        /// Teams Workflows webhooks, keyed by the credential name that a Teams connection refers to.
        /// Set per environment as a secret. Never commit it. Names are 1 to 32 letters or digits.
        /// </summary>
        public Dictionary<string, TeamsCredentialDefinition> Credentials { get; set; } = new();

        /// <summary>
        /// Host patterns a webhook URL may use. When none are configured, the built-in Workflows hosts apply.
        /// </summary>
        public string[]? AllowedHosts { get; set; }

        /// <summary>
        /// When true, a webhook host outside AllowedHosts blocks the post instead of only warning. Default false.
        /// </summary>
        public bool StrictHosts { get; set; }

        /// <summary>
        /// Hosts a card button may link to, besides this site's own domains.
        /// </summary>
        public string[]? AllowedLinkHosts { get; set; }

        /// <summary>
        /// The largest payload sent, in UTF-8 bytes.
        /// </summary>
        public int MaxPayloadBytes { get; set; } = 28000;

        /// <summary>
        /// The longest bound value kept, in characters. Longer values are shortened. Default 2000.
        /// </summary>
        public int MaxValueLength { get; set; } = 2000;

        /// <summary>
        /// The timeout for each Teams request, in seconds. Default 15; clamped to 1..100.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 15;

        /// <summary>
        /// The Adaptive Card version declared when a card has none.
        /// </summary>
        public string DefaultCardVersion { get; set; } = "1.4";

        /// <summary>
        /// The Adaptive Card versions the connection offers. When none are configured, the built-in list applies.
        /// </summary>
        public string[]? CardVersions { get; set; }

        /// <summary>
        /// The per-webhook limiter that keeps steps under the Workflows request limit.
        /// </summary>
        public RateLimitDefinition? RateLimit { get; set; }
    }

    public sealed class TeamsCredentialDefinition
    {
        /// <summary>
        /// The Workflows webhook URL for one channel. Set per environment as a secret. Never commit it.
        /// </summary>
        public string? WebhookUrl { get; set; }
    }

    public sealed class RateLimitDefinition
    {
        /// <summary>
        /// Sustained requests a second for each webhook.
        /// </summary>
        public int RequestsPerSecond { get; set; } = 3;

        /// <summary>
        /// Burst size for each webhook.
        /// </summary>
        public int Burst { get; set; } = 4;

        /// <summary>
        /// The longest a step waits for a token, in seconds.
        /// </summary>
        public int MaxWaitSeconds { get; set; } = 5;
    }
}
