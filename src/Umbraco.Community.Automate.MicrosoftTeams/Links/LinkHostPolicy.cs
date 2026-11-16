using Microsoft.Extensions.Options;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Http;

namespace Umbraco.Community.Automate.MicrosoftTeams.Links;

/// <summary>Whether a card button's link is kept (F04).</summary>
internal sealed record LinkDecision(bool IsAllowed, string? Reason)
{
    public static LinkDecision Allowed { get; } = new(true, null);

    public static LinkDecision Dropped(string reason) => new(false, reason);
}

/// <summary>https only, and only to this site's hosts or <see cref="TeamsOptions.AllowedLinkHosts"/>, unless the author allows external links.</summary>
internal sealed class LinkHostPolicy(SiteHostProvider siteHosts, IOptionsMonitor<TeamsOptions> options)
{
    /// <summary>
    /// Reads the site hosts once (they need an async lookup) and returns a synchronous checker, because the payload
    /// fitter renders the card many times and each render checks its links.
    /// </summary>
    public async Task<LinkChecker> CreateCheckerAsync(CancellationToken cancellationToken = default) =>
        new(await siteHosts.GetSiteHostsAsync(cancellationToken), options.CurrentValue.AllowedLinkHosts);
}

/// <summary>A snapshot of the link rule for one step run.</summary>
internal sealed class LinkChecker(IReadOnlyList<(string Host, bool IsHttp)> siteHosts, IEnumerable<string> allowedLinkHosts)
{
    /// <summary>
    /// Longest link kept. Browsers and Teams clients cope with about this much, and the limit stops a bound value from
    /// filling the 28 KB payload through a link that the fitter's empty-data render would still carry (F01).
    /// </summary>
    public const int MaxLinkLength = 2048;

    public const string TooLong = "a link longer than 2,048 characters";

    public const string NotHttps = "not an absolute https link";

    public const string OutsideAllowList = "links to a host outside AllowedLinkHosts";

    public LinkDecision Check(string? url, bool allowExternal)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !url.Contains("://", StringComparison.Ordinal)
            || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.UserInfo.Length > 0)
        {
            return LinkDecision.Dropped(NotHttps);
        }

        if (url.Trim().Length > MaxLinkLength)
        {
            // Never the value: it is the reason that reaches warnings and logs.
            return LinkDecision.Dropped(TooLong);
        }

        var host = HostNames.Normalise(uri.IdnHost);
        var site = siteHosts.Where(s => s.Host == host).ToList();

        // http is for local development only: the matched site host must itself be http.
        var schemeOk = uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && site.Any(s => s.IsHttp));
        if (!schemeOk)
        {
            return LinkDecision.Dropped(NotHttps);
        }

        if (site.Count > 0 || allowExternal || HostNames.Matches(host, allowedLinkHosts))
        {
            return LinkDecision.Allowed;
        }

        return LinkDecision.Dropped(OutsideAllowList);
    }
}
