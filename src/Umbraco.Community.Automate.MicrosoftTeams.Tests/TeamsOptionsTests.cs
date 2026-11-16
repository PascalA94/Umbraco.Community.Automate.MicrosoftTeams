using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class TeamsOptionsTests
{
    private static TeamsOptions Resolve(Dictionary<string, string?> values)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddOptions<TeamsOptions>().Bind(config.GetSection(TeamsOptions.SectionName));
        services.AddSingleton<IPostConfigureOptions<TeamsOptions>, TeamsOptionsPostConfigure>();
        return services.BuildServiceProvider().GetRequiredService<IOptions<TeamsOptions>>().Value;
    }

    [Fact]
    public void Options_ConfiguredAllowedHosts_ReplaceDefaults()
    {
        var o = Resolve(new() { [$"{TeamsOptions.SectionName}:AllowedHosts:0"] = "a.example" });
        Assert.Equal(["a.example"], o.AllowedHosts);
    }

    [Fact]
    public void Options_NoConfig_UsesDefaults()
    {
        var o = Resolve([]);
        Assert.Equal(TeamsWorkflowsFacts.DefaultAllowedHosts, o.AllowedHosts);
        Assert.False(o.StrictHosts);
        Assert.Equal(2000, o.MaxValueLength);
        Assert.Equal(28000, o.MaxPayloadBytes);
        Assert.Equal(15, o.TimeoutSeconds);
        Assert.Equal(TeamsWorkflowsFacts.DefaultCardVersion, o.DefaultCardVersion);
        Assert.Equal(TeamsWorkflowsFacts.CardVersions, o.CardVersions);
        Assert.Equal((3, 4, 5), (o.RateLimit.RequestsPerSecond, o.RateLimit.Burst, o.RateLimit.MaxWaitSeconds));
        Assert.Empty(o.AllowedLinkHosts);
    }

    [Fact]
    public void Options_MaxWaitClampedToTimeout()
    {
        var o = Resolve(new()
        {
            [$"{TeamsOptions.SectionName}:TimeoutSeconds"] = "2",
            [$"{TeamsOptions.SectionName}:RateLimit:MaxWaitSeconds"] = "30",
            [$"{TeamsOptions.SectionName}:MaxValueLength"] = "0",
            [$"{TeamsOptions.SectionName}:RateLimit:Burst"] = "0",
        });
        Assert.Equal(2, o.RateLimit.MaxWaitSeconds);
        Assert.Equal(1, o.MaxValueLength);
        Assert.Equal(1, o.RateLimit.Burst);
    }

    [Fact]
    public void Options_BindingNeverReadsCredentials()
    {
        var o = Resolve(new()
        {
            [$"{TeamsOptions.SectionName}:Credentials:x:WebhookUrl"] = "https://SENTINELSIG.example/x", // gitleaks:allow
        });
        var text = System.Text.Json.JsonSerializer.Serialize(o);
        Assert.DoesNotContain("SENTINEL", text);
        Assert.DoesNotContain(typeof(TeamsOptions).GetProperties(), p => p.Name.Contains("Credential", StringComparison.OrdinalIgnoreCase));
    }
}
