using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Connection;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>Posts a built-in or custom Adaptive Card to a Teams channel through the connection's Workflows webhook.</summary>
[Action(
    "community.teams.postCard",
    "Post Teams Adaptive Card",
    Description = "Posts an Adaptive Card to a Teams channel through a Workflows webhook.",
    Group = "Messaging",
    Icon = "icon-message",
    ConnectionTypeAlias = "community.teams.workflowWebhook")]
public sealed class PostCardAction : ActionBase<PostCardSettings, TeamsPostOutput>, IValidatableStepType, IPublishValidatableStepType
{
    private const string DefaultLinkLabel = "Open";
    // Reserved fitter key for the Summary. A placeholder key can't contain parentheses, so a Card data row can't reach it.
    private const string SummaryKey = "(summary)";

    // The preview line has no use for more, and it is written to the envelope and to every attachment's fallbackText.
    private const int MaxSummaryLength = 250;

    private const string SubmitWarning = "The card has an Action.Submit button, which Teams Workflows doesn't support.";
    private const string LinkRemovedPrefix = "The 'Action.OpenUrl' link was removed:";

    private readonly IServiceScopeFactory _scopes;

    /// <summary>Creates the action. Automate's services are internal, so they are resolved per run from a scope.</summary>
    public PostCardAction(ActionInfrastructure infrastructure, IServiceScopeFactory scopes)
        : base(infrastructure)
    {
        _scopes = scopes;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;
        var options = services.GetRequiredService<IOptionsMonitor<TeamsOptions>>().CurrentValue;
        var templates = services.GetRequiredService<CardTemplates>();
        var settings = context.GetSettings<PostCardSettings>();
        var connection = context.Connection!.GetSettings<TeamsWorkflowConnectionSettings>();
        var source = settings.CardSource;

        // The save check has already run, but a settings file can bypass it: defence in depth, nothing is sent.
        JsonNode template;
        if (source == CardSource.Custom)
        {
            var card = CustomCardJson.TryParse(settings.CustomCardJson, out var parseError);
            if (card is null)
            {
                return Failed(context, StepRunErrorCategory.Validation, parseError!);
            }

            template = card;
        }
        else
        {
            template = templates.Get(source);
        }

        var warnings = new List<string>();
        var data = Collect(settings, source, templates, options.MaxValueLength, warnings);
        if (source == CardSource.Custom && CustomCardJson.HasActionSubmit(template))
        {
            warnings.Add(SubmitWarning);
        }

        var links = await services.GetRequiredService<LinkHostPolicy>().CreateCheckerAsync(cancellationToken);
        var renderer = services.GetRequiredService<ICardRenderer>();
        var version = string.IsNullOrWhiteSpace(connection.DefaultCardVersion) ? options.DefaultCardVersion : connection.DefaultCardVersion;
        data[SummaryKey] = CollectSummary(settings.Summary, warnings);
        var renderOptions = new CardRenderOptions(
            settings.EscapeMarkdownInData,
            version,
            null,
            CardTemplates.EffectiveAllowExternalLinks(source, settings.AllowExternalLinks));

        // The Summary is a fitter value like any other, so oversized data can never fail the step through it (F01).
        var fit = PayloadFitter.Fit(
            data,
            d => renderer.Render(
                template,
                d.Where(kv => kv.Key != SummaryKey).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
                renderOptions with { Summary = d[SummaryKey] },
                links).ToRenderOutput(),
            options.MaxPayloadBytes);
        if (fit.TemplateTooBig)
        {
            return Failed(context, StepRunErrorCategory.Validation, $"Card is {fit.Bytes} bytes before any data is added. Teams allows about {options.MaxPayloadBytes / 1000} KB. Shorten the template.");
        }

        var renderWarnings = fit.Warnings.Select(w => w.Replace("'" + SummaryKey + "'", "'Summary'"));
        if (source == CardSource.Simple && string.IsNullOrWhiteSpace(data["url"]))
        {
            // A Simple card without a url has no button by design, so that is not worth a warning on every run.
            renderWarnings = renderWarnings.Where(w => !w.StartsWith(LinkRemovedPrefix, StringComparison.Ordinal));
        }

        warnings.AddRange(renderWarnings);

        var result = await services.GetRequiredService<ITeamsWebhookSender>().PostAsync(connection.CredentialName, fit.Envelope!, cancellationToken);
        foreach (var warning in warnings.Concat(result.Warnings).Distinct(StringComparer.Ordinal))
        {
            // Text only: keys and fixed phrases, never a value, link or host.
            context.LogWarning(warning);
        }

        if (result.Failure is { } failure)
        {
            return Failed(context, failure.Category, failure.Message);
        }

        return Success(TeamsPostOutput.From(result, warnings));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ValidateSettingsAsync(object? settings, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (settings is PostCardSettings s)
        {
            if (s.CardSource == CardSource.Custom && CustomCardJson.TryParse(s.CustomCardJson, out var error) is null)
            {
                errors.Add(error!);
            }

            errors.AddRange(s.CardData
                .Select(r => r.Key?.Trim())
                .Where(k => !string.IsNullOrEmpty(k))
                .GroupBy(k => k!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => $"Card data has the key '{g.Key}' more than once."));
        }

        return Task.FromResult<IReadOnlyList<string>>(errors);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ValidateSettingsForPublishAsync(object? settings, Automation automation, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (settings is PostCardSettings { CardSource: not CardSource.Custom } s)
        {
            // The key table is immutable, so a throwaway instance is as good as the registered one.
            var templates = new CardTemplates();
            var filled = s.CardData
                .Where(r => !string.IsNullOrWhiteSpace(r.Key) && !string.IsNullOrWhiteSpace(r.Value))
                .Select(r => r.Key.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            errors.AddRange(templates.RequiredKeys(s.CardSource)
                .Where(k => !filled.Contains(k))
                .Select(k => $"{templates.DisplayName(s.CardSource)} card needs a {k} value"));
        }

        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;
        errors.AddRange(await ConnectionChoiceValidator.ValidateAsync(
            automation,
            services.GetRequiredService<IWorkspaceService>(),
            services.GetRequiredService<IConnectionService>(),
            cancellationToken));
        return errors;
    }

    // Caps every value and builds the fitter's data. Long or blank values are fixed here, never failed (F01).
    private static Dictionary<string, string> Collect(PostCardSettings settings, CardSource source, CardTemplates templates, int maxLength, List<string> warnings)
    {
        var builtInKeys = templates.AllKeys(source);
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in settings.CardData)
        {
            var key = row.Key is null ? null : ValueLimiter.ReplaceLoneSurrogates(row.Key).Trim();
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (source != CardSource.Custom && !builtInKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                warnings.Add($"Card key '{key}' isn't used by the {templates.DisplayName(source)} template, so it wasn't sent.");
                continue;
            }

            var (capped, truncated) = ValueLimiter.Truncate(row.Value, maxLength);
            if (truncated)
            {
                warnings.Add($"Card value '{key}' was shortened to fit.");
            }

            data[key] = capped;
        }

        // An optional key the author left out is just blank: no "unknown key" warning for a documented key.
        foreach (var key in builtInKeys.Where(k => !data.ContainsKey(k)))
        {
            data[key] = string.Empty;
        }

        if (source == CardSource.Simple && !string.IsNullOrWhiteSpace(data["url"]) && string.IsNullOrWhiteSpace(data["linkLabel"]))
        {
            data["linkLabel"] = DefaultLinkLabel;
        }

        return data;
    }

    private static string CollectSummary(string? summary, List<string> warnings)
    {
        var (capped, truncated) = ValueLimiter.Truncate(ValueLimiter.StripControlAndBidi(summary ?? string.Empty), MaxSummaryLength);
        if (truncated)
        {
            warnings.Add("Card value 'Summary' was shortened to fit.");
        }

        return capped;
    }

    // Our own exception, with no inner exception and a message we wrote (design 7.5).
    private static ActionResult Failed(ActionContext context, StepRunErrorCategory category, string message)
    {
        context.LogError(message);
        return ActionResult.Failed(new TeamsPostException(message), category);
    }
}
