using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>An in-memory JSON configuration source that reports a file name as its path, without touching disk.</summary>
internal static class JsonFileConfig
{
    public static IConfigurationRoot Build(string fileName, string json) => new ConfigurationBuilder().Add(new Source(fileName, json)).Build();

    private sealed class Source(string file, string json) : JsonConfigurationSource
    {
        public override IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            Path = file;
            return new Provider(this, json);
        }
    }

    private sealed class Provider(Source source, string json) : JsonConfigurationProvider(source)
    {
        public override void Load() => Load(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
