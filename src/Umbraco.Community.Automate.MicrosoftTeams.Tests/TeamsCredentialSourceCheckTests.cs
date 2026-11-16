using System.Text;
using Microsoft.Extensions.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class TeamsCredentialSourceCheckTests
{
    private const string Key = "Umbraco:Automate:MicrosoftTeams:Credentials:alerts:WebhookUrl";
    private const string Json = """{"Umbraco":{"Automate":{"MicrosoftTeams":{"Credentials":{"alerts":{"WebhookUrl":"https://SENTINELSIG.example.invalid/x"}}}}}}"""; // gitleaks:allow

    private static IConfigurationBuilder WithJson(string file)
    {
        var builder = new ConfigurationBuilder();
        builder.Add(new TestJsonSource(file, Json));
        return builder;
    }

    private static string? Check(IConfigurationRoot root) =>
        new TeamsCredentialSourceCheck(root, TestCredentials.Store(root)).AppsettingsFileSupplying("alerts");

    [Fact]
    public void AppsettingsJson_ReturnsFileName_NotValue()
    {
        var result = Check(WithJson("appsettings.Development.json").Build());
        Assert.Equal("appsettings.Development.json", result);
        Assert.DoesNotContain("SENTINEL", result);
    }

    [Fact]
    public void EnvironmentVariables_ReturnsNull() =>
        Assert.Null(Check(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "x" }).Build()));

    [Fact]
    public void UserSecretsJson_ReturnsNull() => Assert.Null(Check(WithJson("secrets.json").Build()));

    [Fact]
    public void LaterProviderWins_ReturnsNullWhenEnvOverridesJson() =>
        Assert.Null(Check(WithJson("appsettings.json").AddInMemoryCollection(new Dictionary<string, string?> { [Key] = "other" }).Build()));

    [Fact]
    public void KeyAbsent_ReturnsNull() => Assert.Null(Check(new ConfigurationBuilder().Build()));

    // An in-memory JSON provider that reports a file name as its path, without touching disk.
    private sealed class TestJsonSource(string file, string json) : Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            Path = file;
            return new TestJsonProvider(this, json);
        }
    }

    private sealed class TestJsonProvider(TestJsonSource source, string json) : Microsoft.Extensions.Configuration.Json.JsonConfigurationProvider(source)
    {
        public override void Load() => Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
