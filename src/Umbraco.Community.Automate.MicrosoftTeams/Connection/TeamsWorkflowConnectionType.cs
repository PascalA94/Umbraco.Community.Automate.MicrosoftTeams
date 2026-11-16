using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Connections;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Credentials;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Connection;

/// <summary>The <c>community.teams.workflowWebhook</c> connection (aliases are permanent once released).</summary>
[ConnectionType(
    "community.teams.workflowWebhook",
    "Microsoft Teams (Workflows webhook)",
    Description = "Posts to a Teams channel through a Workflows webhook whose URL is kept in server configuration, never on the connection. Setup: https://github.com/PascalA94/Umbraco.Community.Automate.MicrosoftTeams#setup",
    Group = "Messaging",
    Icon = "icon-message")]
public sealed class TeamsWorkflowConnectionType : ConnectionTypeBase<TeamsWorkflowConnectionSettings>
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// Creates the connection type. Automate builds it through DI with a public constructor, and the services it needs
    /// are internal, so they are resolved per validation from a scope. Nothing is resolved at construction.
    /// </summary>
    public TeamsWorkflowConnectionType(ConnectionTypeInfrastructure infrastructure, IServiceScopeFactory scopes)
        : base(infrastructure)
    {
        _scopes = scopes;
    }

    /// <inheritdoc />
    public override async Task<ConnectionValidationResult> ValidateAsync(object? settings, CancellationToken cancellationToken)
    {
        if (settings is not TeamsWorkflowConnectionSettings typed)
        {
            return ConnectionValidationResult.Failure("Enter the credential name.");
        }

        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;
            var sender = services.GetRequiredService<ITeamsWebhookSender>();

            var applicationUrl = services.GetRequiredService<IOptionsMonitor<WebRoutingSettings>>().CurrentValue.UmbracoApplicationUrl;
            var site = Uri.TryCreate(applicationUrl, UriKind.Absolute, out var siteUri) && HostNames.IsDnsName(siteUri.IdnHost)
                ? siteUri.IdnHost
                : "your Umbraco site";

            var result = await sender.PostAsync(typed.CredentialName, TestCard(site), cancellationToken);
            if (result.Failure is { } failure)
            {
                return ConnectionValidationResult.Failure(failure.Message);
            }

            var parts = new List<string>
            {
                result.TestWarnings.Count == 0
                    ? "Connected (Teams accepted the message)."
                    : $"Connected, but {LowerFirstThe(result.TestWarnings[0])} Check the URL.",
                "Anyone who can build automations in a workspace that allows this connection can post to this channel.",
            };

            // Spike S3 is unconfirmed on how the backoffice shows ConnectionValidationStatus.Warning, so a warning is
            // appended to a Success: it reads the same whichever way Warning is rendered.
            var store = services.GetRequiredService<TeamsCredentialStore>();
            if (store.TryNormaliseName(typed.CredentialName, out var normalised, out _)
                && services.GetRequiredService<TeamsCredentialSourceCheck>().AppsettingsFileSupplying(normalised!) is { } file)
            {
                parts.Add($"Warning: Credential '{normalised}' is read from {file}. Move it to environment variables, Umbraco Cloud secrets or Key Vault.");
            }

            if ((await services.GetRequiredService<SiteHostProvider>().GetSiteHostsAsync(cancellationToken)).Count == 0)
            {
                parts.Add("Warning: no site domain is configured, so card buttons are dropped. Set Umbraco:CMS:WebRouting:UmbracoApplicationUrl or add a domain.");
            }

            return ConnectionValidationResult.Success(string.Join(" ", parts));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Never pass on the exception's message: Core's own fallback would show it, and it can hold the URL.
            return ConnectionValidationResult.Failure("Couldn't test the connection.");
        }
    }

    private static string LowerFirstThe(string text) => text.StartsWith("The ", StringComparison.Ordinal) ? "t" + text[1..] : text;

    private static JsonObject TestCard(string site) => CardEnvelope.Wrap(new JsonObject
    {
        ["type"] = "AdaptiveCard",
        ["version"] = TeamsWorkflowsFacts.DefaultCardVersion,
        ["body"] = new JsonArray(new JsonObject
        {
            ["type"] = "TextBlock",
            ["text"] = $"Connection test from {MarkdownEscaper.Escape(site)}",
            ["wrap"] = true,
        }),
    });
}
