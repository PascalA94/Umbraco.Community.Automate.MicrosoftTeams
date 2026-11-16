using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Automate.MicrosoftTeams.Http;

namespace Umbraco.Community.Automate.MicrosoftTeams.Links;

/// <summary>This site's own hosts: the application URL plus the domains assigned in the content tree. Cached for five minutes.</summary>
internal sealed class SiteHostProvider(IOptionsMonitor<WebRoutingSettings> routing, IDomainService domains, TimeProvider timeProvider)
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(5);

    // Instance fields, never statics: the provider is a singleton and holds no request-scoped state.
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<(string Host, bool IsHttp)> _hosts = [];
    private DateTimeOffset _expires = DateTimeOffset.MinValue;

    // GetAllAsync is the only domain listing on both lines (Umbraco 18 removed the synchronous GetAll).
    public async Task<IReadOnlyList<(string Host, bool IsHttp)>> GetSiteHostsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (now < _expires)
            {
                return _hosts;
            }

            var found = new List<(string Host, bool IsHttp)>();
            Add(found, routing.CurrentValue.UmbracoApplicationUrl);
            foreach (var domain in await domains.GetAllAsync(includeWildcards: false))
            {
                Add(found, domain.DomainName);
            }

            _hosts = found.DistinctBy(h => (h.Host, h.IsHttp)).ToList();
            _expires = now + CacheFor;
            return _hosts;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void Add(List<(string Host, bool IsHttp)> found, string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var text = raw.Trim();

        // A content-tree domain may be "example.com", "example.com/en" or "https://example.com". With no scheme we
        // assume https: an http link needs a site that says it is http.
        var hasScheme = text.Contains("://", StringComparison.Ordinal);
        if (Uri.TryCreate(hasScheme ? text : "https://" + text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.IdnHost))
        {
            found.Add((HostNames.Normalise(uri.IdnHost), hasScheme && uri.Scheme == Uri.UriSchemeHttp));
        }
    }
}
