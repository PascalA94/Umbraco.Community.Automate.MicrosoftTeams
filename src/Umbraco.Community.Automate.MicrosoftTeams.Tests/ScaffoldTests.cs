using System.Reflection;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class ScaffoldTests
{
    [Fact]
    public void Assembly_IsBuiltForTheSelectedAutomateMajor()
    {
        var major = typeof(TeamsAssemblyMarker).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "AutomateMajor").Value;
#if AUTOMATE_17
        Assert.Equal("17", major);
#else
        Assert.Equal("18", major);
#endif
    }
}
