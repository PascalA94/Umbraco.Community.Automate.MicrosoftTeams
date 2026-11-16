using Microsoft.Extensions.Options;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>The result of checking a webhook URL. <see cref="Warning"/> never names the host; <see cref="TestWarning"/> may.</summary>
internal sealed record WebhookUrlCheck(ValidatedWebhookUrl? Url, string? Error, string? Warning, string? TestWarning)
{
    public bool IsBlocked => Url is null;
}

/// <summary>The design's section 7.6 checks, in order, over the resolved credential value.</summary>
internal sealed class WebhookUrlValidator(IOptionsMonitor<TeamsOptions> options)
{
    private const string UnknownHostGeneric = "The webhook host isn't a known Power Automate host.";

    private static readonly IReadOnlyList<string> RetiredHosts = ["*.webhook.office.com"];

    public WebhookUrlCheck Check(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Blocked("Webhook URL is required.");
        }

        // On Unix, "/relative/path" parses as an absolute file URI, so insist on a scheme separator.
        if (!url.Contains("://", StringComparison.Ordinal) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return Blocked("This isn't a valid URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return Blocked("Teams webhook URLs must use https.");
        }

        var host = HostNames.Normalise(uri.IdnHost);
        if (HostNames.Matches(host, RetiredHosts))
        {
            return Blocked($"This is a retired Office 365 connector URL. Create a Workflows webhook instead. See {TeamsDocs.SetupUrl}");
        }

        var current = options.CurrentValue;
        if (HostNames.Matches(host, current.AllowedHosts))
        {
            return new WebhookUrlCheck(new ValidatedWebhookUrl(uri, host), null, null, null);
        }

        if (current.StrictHosts)
        {
            return Blocked("The webhook host isn't in AllowedHosts.");
        }

        var testWarning = HostNames.IsDnsName(host) ? $"{host} isn't a known Power Automate host." : UnknownHostGeneric;
        return new WebhookUrlCheck(new ValidatedWebhookUrl(uri, host), null, UnknownHostGeneric, testWarning);
    }

    private static WebhookUrlCheck Blocked(string error) => new(null, error, null, null);
}
