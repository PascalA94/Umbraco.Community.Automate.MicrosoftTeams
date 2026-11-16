using Microsoft.Extensions.Options;
using NSubstitute;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Links;
using Microsoft.Extensions.Time.Testing;
using CredentialHelper = CredentialHelperContract.TestOptionsMonitor<Umbraco.Community.Automate.MicrosoftTeams.Configuration.TeamsOptions>;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class LinkHostPolicyTests
{
    private static (LinkHostPolicy Policy, IDomainService Domains, FakeTimeProvider Clock) Make(string? appUrl, string[]? domains = null, string[]? allowed = null)
    {
        var routing = Substitute.For<IOptionsMonitor<WebRoutingSettings>>();
        routing.CurrentValue.Returns(new WebRoutingSettings { UmbracoApplicationUrl = appUrl });
        var domainService = Substitute.For<IDomainService>();
        domainService.GetAllAsync(false).Returns(_ => Task.FromResult<IEnumerable<IDomain>>((domains ?? []).Select(d =>
        {
            var domain = Substitute.For<IDomain>();
            domain.DomainName.Returns(d);
            return domain;
        }).ToList()));
        var clock = new FakeTimeProvider();
        var options = new TeamsOptions { AllowedLinkHosts = allowed ?? [] };
        var policy = new LinkHostPolicy(new SiteHostProvider(routing, domainService, clock), new CredentialHelper(options));
        return (policy, domainService, clock);
    }

    private static async Task<LinkDecision> Check(LinkHostPolicy p, string? url, bool allowExternal = false) =>
        (await p.CreateCheckerAsync()).Check(url, allowExternal);

    [Fact]
    public async Task Check_SiteHost_Allowed() => Assert.True((await Check(Make("https://www.example.com").Policy, "https://www.example.com/news")).IsAllowed);

    [Fact]
    public async Task Check_DomainFromContentTree_Allowed() => Assert.True((await Check(Make(null, ["shop.example.com/en"]).Policy, "https://shop.example.com/x")).IsAllowed);

    [Fact]
    public async Task Check_ConfiguredAllowedLinkHost_Wildcard_Allowed() => Assert.True((await Check(Make("https://a.example", allowed: ["*.partner.example"]).Policy, "https://x.partner.example/p")).IsAllowed);

    [Fact]
    public async Task Check_ExternalHost_Dropped()
    {
        var d = await Check(Make("https://a.example").Policy, "https://evil.example/x");
        Assert.False(d.IsAllowed);
        Assert.Equal("links to a host outside AllowedLinkHosts", d.Reason);
    }

    [Fact]
    public async Task Check_ExternalHost_AllowExternal_Allowed() => Assert.True((await Check(Make("https://a.example").Policy, "https://evil.example/x", true)).IsAllowed);

    [Fact]
    public async Task Check_HttpExternal_Dropped()
    {
        var d = await Check(Make("https://a.example").Policy, "http://evil.example/x", true);
        Assert.False(d.IsAllowed);
        Assert.Equal("not an absolute https link", d.Reason);
    }

    [Fact]
    public async Task Check_HttpSiteHostOnLocalHttpSite_Allowed() => Assert.True((await Check(Make("http://localhost:5000").Policy, "http://localhost:5000/x")).IsAllowed);

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("/relative")]
    [InlineData("")]
    [InlineData("https://user:pw@a.example/x")]
    public async Task Check_NonHttps_DroppedEvenWithAllowExternal(string url)
    {
        var d = await Check(Make("https://a.example").Policy, url, true);
        Assert.False(d.IsAllowed);
        Assert.Equal("not an absolute https link", d.Reason);
    }

    [Fact]
    public async Task Check_IdnLookAlikeOfSiteHost_Dropped() => Assert.False((await Check(Make("https://example.com").Policy, "https://ехample.com/x")).IsAllowed);

    [Fact]
    public async Task SiteHosts_CachedForFiveMinutes()
    {
        var (policy, domains, clock) = Make("https://a.example", ["b.example"]);
        await Check(policy, "https://a.example/");
        await Check(policy, "https://a.example/");
        await domains.Received(1).GetAllAsync(false);
        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        await Check(policy, "https://a.example/");
        await domains.Received(2).GetAllAsync(false);
    }

    [Fact]
    public async Task SiteHosts_NoneConfigured_EmptyList()
    {
        var routing = Substitute.For<IOptionsMonitor<WebRoutingSettings>>();
        routing.CurrentValue.Returns(new WebRoutingSettings());
        var domains = Substitute.For<IDomainService>();
        domains.GetAllAsync(false).Returns(Task.FromResult<IEnumerable<IDomain>>([]));
        Assert.Empty(await new SiteHostProvider(routing, domains, TimeProvider.System).GetSiteHostsAsync());
    }
}
