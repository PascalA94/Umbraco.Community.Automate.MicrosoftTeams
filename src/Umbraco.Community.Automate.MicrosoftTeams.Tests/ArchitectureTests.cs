using System.Collections.Frozen;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CredentialHelperContract;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Settings;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Web;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class ArchitectureTests
{
    private static readonly Assembly Package = typeof(TeamsAssemblyMarker).Assembly;

    private static IEnumerable<Type> PackageTypes => Package.GetTypes();

    [Fact]
    public void NoSettingsType_HasSensitiveField()
    {
        var sensitive = PackageTypes.SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(p => p.GetCustomAttribute<FieldAttribute>()?.IsSensitive == true)
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}").ToList();
        Assert.Empty(sensitive);
    }

    // P2: no static field holds request-scoped or mutable state. Anything else needs a reason here.
    private static readonly FrozenSet<Type> ImmutableStaticTypes = new[]
    {
        typeof(string), typeof(Regex), typeof(IReadOnlyList<string>), typeof(FrozenSet<string>),
        typeof(TimeSpan), typeof(System.Globalization.IdnMapping), // value type, stateless
        typeof(System.Text.Json.JsonSerializerOptions), // configured once in an initialiser, read-only once used
        typeof(Umbraco.Community.Automate.MicrosoftTeams.Links.LinkDecision), // an immutable record
    }.ToFrozenSet();

    [Fact]
    public void NoStaticField_HoldsScopedOrMutableState()
    {
        var offenders = new List<string>();
        foreach (var type in PackageTypes.Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() is null && !t.Name.StartsWith('<') && t.Namespace?.Contains("RegularExpressions.Generated", StringComparison.Ordinal) != true))
        {
            foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                var constant = field.IsLiteral;
                var readonlyAllowed = field.IsInitOnly && ImmutableStaticTypes.Contains(field.FieldType);
                if (!constant && !readonlyAllowed)
                {
                    offenders.Add($"{type.Name}.{field.Name} ({field.FieldType.Name})");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Composer_RegistersOnlySingletons_WithSingletonSafeDependencies()
    {
        var services = new ServiceCollection();
        new MicrosoftTeamsComposer().Compose(AutomateRegistration.CreateBuilder(services, new ConfigurationBuilder().Build()));

        var allowedExternal = new[] { typeof(IHttpClientFactory), typeof(IConfiguration), typeof(TimeProvider), typeof(Umbraco.Cms.Core.Services.IDomainService) };
        var ownServices = services.Where(d => (d.ImplementationType ?? d.ServiceType).Assembly == Package).ToList();
        Assert.NotEmpty(ownServices);
        foreach (var descriptor in ownServices.Where(d => d.ImplementationType is not null))
        {
            // Notification handlers are transient by Umbraco's own registration; this one holds only the singleton store and a logger.
            var isHandler = descriptor.ServiceType.IsGenericType && descriptor.ServiceType.GetGenericTypeDefinition() == typeof(Umbraco.Cms.Core.Events.INotificationHandler<>);
            Assert.True(isHandler || descriptor.Lifetime == ServiceLifetime.Singleton, $"{descriptor.ImplementationType!.Name} is {descriptor.Lifetime}");
            foreach (var parameter in descriptor.ImplementationType!.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().GetParameters())
            {
                var t = parameter.ParameterType;
                var generic = t.IsGenericType ? t.GetGenericTypeDefinition() : null;
                var ok = t.Assembly == Package
                    || allowedExternal.Contains(t)
                    || generic == typeof(Microsoft.Extensions.Options.IOptionsMonitor<>)
                    || generic == typeof(Microsoft.Extensions.Options.IOptions<>)
                    || generic == typeof(Microsoft.Extensions.Logging.ILogger<>);
                Assert.True(ok, $"{descriptor.ImplementationType.Name} takes {t.Name}");
            }
        }
    }

    [Fact]
    public void Composer_RegistersStoreStartupCheckSenderAndLimiter()
    {
        var services = new ServiceCollection();
        new MicrosoftTeamsComposer().Compose(AutomateRegistration.CreateBuilder(services, new ConfigurationBuilder().Build()));

        // Contract section 1.1: the startup check only runs if the composer registers it.
        Assert.Contains(services, d => d.ServiceType == typeof(TeamsCredentialStore) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(services, d => d.ServiceType == typeof(Umbraco.Cms.Core.Events.INotificationHandler<Umbraco.Cms.Core.Notifications.UmbracoApplicationStartedNotification>)
            && d.ImplementationType == typeof(TeamsCredentialStoreStartupCheck));
        Assert.Contains(services, d => d.ServiceType == typeof(ITeamsWebhookSender) && d.ImplementationType == typeof(TeamsWebhookSender));
        Assert.Contains(services, d => d.ServiceType == typeof(WebhookRateLimiter));
    }

    [Fact]
    public void Assembly_HasNoControllersOrViews()
    {
        var controllerNames = new[] { "ControllerBase", "Controller", "SurfaceController", "UmbracoApiController", "UmbracoApiControllerBase", "ManagementApiControllerBase" };
        var controllers = PackageTypes.Where(t => BaseChain(t).Any(b => controllerNames.Contains(b.Name))).Select(t => t.Name).ToList();
        Assert.Empty(controllers);

        var views = PackageTypes.Where(t => t.Name.EndsWith("View", StringComparison.Ordinal) && t.GetCustomAttributes().Any(a => a.GetType().Name.Contains("RazorSourceChecksum", StringComparison.Ordinal))).ToList();
        Assert.Empty(views);
        Assert.DoesNotContain(Package.GetManifestResourceNames(), n => n.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NoSettings_HasWebhookStyleUrlProperty()
    {
        var offenders = PackageTypes
            .Where(t => t.Namespace is not null && (t.Namespace.EndsWith(".Actions", StringComparison.Ordinal) || t.Namespace.EndsWith(".Connection", StringComparison.Ordinal)))
            .SelectMany(t => t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            .Where(p => p.Name.Contains("Url", StringComparison.OrdinalIgnoreCase) && !(p.DeclaringType!.Name == "PostMessageSettings" && p.Name == "LinkUrl"))
            .Select(p => $"{p.DeclaringType!.Name}.{p.Name}").ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void OnlySender_CallsCredentialStoreTryGet()
    {
        var calls = IlScan.Calls(Package)
            .Where(c => c.Target is { Name: "TryGet" } && (c.Target.DeclaringType == typeof(TeamsCredentialStore)
                || (c.Target.DeclaringType is { IsGenericType: true } d && d.GetGenericTypeDefinition() == typeof(ICredentialStore<>))))
            .Select(c => c.Type.FullName!).Distinct().ToList();

        // Positive control: the scan sees the sender's call.
        Assert.Contains(typeof(TeamsWebhookSender).FullName, calls);
        Assert.DoesNotContain(calls, c => c != typeof(TeamsWebhookSender).FullName && c != typeof(TeamsCredentialStore).FullName);
    }

    [Fact]
    public void TeamsPostException_HasNoInnerExceptionConstructor()
    {
        var ctors = typeof(TeamsPostException).GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var ctor = Assert.Single(ctors);
        Assert.Equal([typeof(string)], ctor.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    private static IEnumerable<Type> BaseChain(Type type)
    {
        for (var b = type.BaseType; b is not null; b = b.BaseType)
        {
            yield return b;
        }
    }
}
