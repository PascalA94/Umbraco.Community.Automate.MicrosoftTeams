namespace Umbraco.Community.Automate.MicrosoftTeams.Configuration;

/// <summary>
/// The only place that encodes how Microsoft Teams Workflows webhooks behave. The package defaults and the test mock
/// both read these members, so the Live gate (plan, "Mock-first, live gate deferred") corrects every assumption in one edit.
/// </summary>
/// <remarks>
/// Every member below is <b>[unverified: from Microsoft docs, not tested live]</b>. The doc links are pointers to where
/// the behaviour is described, to be re-checked at the Live gate. When a fact is verified, change the tag to
/// <c>[verified live &lt;date&gt;]</c> here and in <c>docs/spikes.md</c>. Never put a real webhook URL, host or tenant here.
/// </remarks>
internal static class TeamsWorkflowsFacts
{
    /// <summary>A good post is answered 202 with an empty body. [unverified: from Microsoft docs, not tested live] https://support.microsoft.com/office/8ae491c7-0394-4861-ba59-055e33f75498</summary>
    public const int AcceptedStatusCode = 202;

    /// <summary>The envelope's `type`. [unverified: from Microsoft docs, not tested live] https://learn.microsoft.com/microsoftteams/platform/webhooks-and-connectors/how-to/connectors-using</summary>
    public const string EnvelopeType = "message";

    /// <summary>The attachment content type for an Adaptive Card. [unverified: from Microsoft docs, not tested live] https://learn.microsoft.com/microsoftteams/platform/webhooks-and-connectors/how-to/connectors-using</summary>
    public const string AdaptiveCardContentType = "application/vnd.microsoft.card.adaptive";

    /// <summary>The card version used when a card declares none. [unverified: from Microsoft docs, not tested live] https://learn.microsoft.com/microsoftteams/platform/task-modules-and-cards/cards/cards-reference</summary>
    public const string DefaultCardVersion = "1.4";

    /// <summary>Card versions offered in the connection's dropdown. [unverified: from Microsoft docs, not tested live] same page as above</summary>
    public static readonly IReadOnlyList<string> CardVersions = ["1.2", "1.3", "1.4", "1.5"];

    /// <summary>Default host patterns for Workflows webhook URLs. [unverified: from Microsoft docs, not tested live] the Live gate (S1) records the real pattern</summary>
    public static readonly IReadOnlyList<string> DefaultAllowedHosts = ["*.powerplatform.com", "*.logic.azure.com", "*.azure-apihub.net"];

    /// <summary>The largest body Teams accepts, in UTF-8 bytes (documented as about 28 KB). [unverified: from Microsoft docs, not tested live] https://learn.microsoft.com/microsoftteams/platform/webhooks-and-connectors/how-to/connectors-using</summary>
    public const int MaxPayloadBytes = 28000;

    /// <summary>Throttled posts are answered 429. [unverified: from Microsoft docs, not tested live] https://learn.microsoft.com/microsoftteams/platform/webhooks-and-connectors/how-to/connectors-using</summary>
    public const int ThrottleStatusCode = 429;

    /// <summary>The Retry-After the mock sends with a 429, in seconds. [unverified: from Microsoft docs, not tested live]</summary>
    public const int ThrottleRetryAfterSeconds = 2;

    /// <summary>Documented sustained rate per webhook (about 4 a second); the package default stays below it. [unverified: from Microsoft docs, not tested live]</summary>
    public const int RequestsPerSecond = 3;

    /// <summary>Burst size for the per-webhook limiter. [unverified: from Microsoft docs, not tested live] a package choice under the documented rate</summary>
    public const int Burst = 4;

    /// <summary>Longest the limiter makes a step wait, in seconds. [unverified: from Microsoft docs, not tested live] a package choice</summary>
    public const int MaxWaitSeconds = 5;

    /// <summary>Status for a body that is not valid JSON. [unverified: from Microsoft docs, not tested live]</summary>
    public const int BadJsonStatusCode = 400;

    /// <summary>Body text for bad JSON. The real shape is unknown until the Live gate (S1). [unverified: from Microsoft docs, not tested live]</summary>
    public const string BadJsonBody = "Bad Request: the request body is not valid JSON.";

    /// <summary>Status for a body over the size limit. [unverified: from Microsoft docs, not tested live]</summary>
    public const int OversizeStatusCode = 413;

    /// <summary>Body text for an oversized request. [unverified: from Microsoft docs, not tested live]</summary>
    public const string OversizeBody = "Payload Too Large: the request body is over the size limit.";

    /// <summary>Status for a deleted or turned-off workflow. [unverified: from Microsoft docs, not tested live]</summary>
    public const int WorkflowGoneStatusCode = 404;

    /// <summary>Body text for a deleted or turned-off workflow. [unverified: from Microsoft docs, not tested live]</summary>
    public const string WorkflowGoneBody = "Not Found: the workflow does not exist or is turned off.";
}
