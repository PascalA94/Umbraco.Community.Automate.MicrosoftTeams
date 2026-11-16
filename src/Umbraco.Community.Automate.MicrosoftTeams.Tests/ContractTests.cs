using System.Reflection;
using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Community.Automate.MicrosoftTeams.Actions;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class ContractTests
{
    private static JsonElement Frozen()
    {
        using var stream = typeof(ContractTests).Assembly.GetManifestResourceStream("frozen-contract.json")!;
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    private static string[] Props(Type settings) => settings.GetProperties(BindingFlags.Instance | BindingFlags.Public).Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static string[] Listed(JsonElement e) => e.EnumerateArray().Select(x => x.GetString()!).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    [Fact]
    public void AliasesSettingsAndEnums_MatchFrozenContract()
    {
        var frozen = Frozen();
        var package = typeof(PostMessageAction).Assembly;

        var connections = package.GetTypes().Select(t => (Type: t, Attr: t.GetCustomAttribute<ConnectionTypeAttribute>())).Where(x => x.Attr is not null).ToList();
        Assert.Equal(frozen.GetProperty("connectionTypes").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal), connections.Select(c => c.Attr!.Alias).Order(StringComparer.Ordinal));
        foreach (var (type, attr) in connections)
        {
            var settings = type.BaseType!.GetGenericArguments()[0];
            Assert.Equal(Listed(frozen.GetProperty("connectionTypes").GetProperty(attr!.Alias).GetProperty("settings")), Props(settings));
        }

        var actions = package.GetTypes().Select(t => (Type: t, Attr: t.GetCustomAttribute<ActionAttribute>())).Where(x => x.Attr is not null).ToList();
        Assert.Equal(frozen.GetProperty("actions").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal), actions.Select(a => a.Attr!.Alias).Order(StringComparer.Ordinal));
        foreach (var (type, attr) in actions)
        {
            var entry = frozen.GetProperty("actions").GetProperty(attr!.Alias);
            Assert.Equal(entry.GetProperty("connectionTypeAlias").GetString(), attr.ConnectionTypeAlias);
            Assert.Equal(Listed(entry.GetProperty("settings")), Props(type.BaseType!.GetGenericArguments()[0]));
        }

        foreach (var e in frozen.GetProperty("enums").EnumerateObject())
        {
            var enumType = package.GetTypes().Single(t => t.IsEnum && t.Name == e.Name);
            Assert.Equal(Listed(e.Value), Enum.GetNames(enumType).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }
    }
}
