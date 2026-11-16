using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Connection;
using Umbraco.Community.Automate.MicrosoftTeams.Http;
using Umbraco.Community.Automate.MicrosoftTeams.Links;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>Posts a simple message card to a Teams channel through the connection's Workflows webhook.</summary>
[Action(
    "community.teams.postMessage",
    "Post Teams message",
    Description = "Posts a message to a Teams channel through a Workflows webhook.",
    Group = "Messaging",
    Icon = "icon-message",
    ConnectionTypeAlias = "community.teams.workflowWebhook")]
public sealed class PostMessageAction : ActionBase<PostMessageSettings, TeamsPostOutput>, IValidatableStepType, IPublishValidatableStepType
{
    // Reserved keys for the fitter. A placeholder key can't contain parentheses, so a Data row can't reach them.
    private const string TitleKey = "(title)";
    private const string MessageKey = "(message)";
    private const string BlankMessage = "(no message text)";

    private readonly IServiceScopeFactory _scopes;

    /// <summary>
    /// Creates the action. Automate builds it through DI with a public constructor, and the services it needs are internal,
    /// so they are resolved per run from a scope.
    /// </summary>
    public PostMessageAction(ActionInfrastructure infrastructure, IServiceScopeFactory scopes)
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
        var settings = context.GetSettings<PostMessageSettings>();
        var connection = context.Connection!.GetSettings<TeamsWorkflowConnectionSettings>();

        var warnings = new List<string>();
        var (values, valueWarnings) = Collect(settings, options.MaxValueLength);
        warnings.AddRange(valueWarnings);

        var links = await services.GetRequiredService<LinkHostPolicy>().CreateCheckerAsync(cancellationToken);
        var version = string.IsNullOrWhiteSpace(connection.DefaultCardVersion) ? options.DefaultCardVersion : connection.DefaultCardVersion;

        var fit = PayloadFitter.Fit(
            values,
            d => MessageCardBuilder.Build(
                new PostMessageSettings
                {
                    Title = d[TitleKey],
                    Message = d[MessageKey],
                    Data = [],
                    LinkUrl = settings.LinkUrl,
                    LinkLabel = settings.LinkLabel,
                    Style = settings.Style,
                    Formatting = settings.Formatting,
                    AllowExternalLinks = settings.AllowExternalLinks,
                },
                d.Where(kv => kv.Key is not (TitleKey or MessageKey)).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
                version,
                links),
            options.MaxPayloadBytes);

        if (fit.TemplateTooBig)
        {
            return Failed(context, StepRunErrorCategory.Validation, $"Card is {fit.Bytes} bytes before any data is added. Teams allows about {options.MaxPayloadBytes / 1000} KB. Shorten the template.");
        }

        warnings.AddRange(fit.Warnings.Select(w => w.Replace("'" + TitleKey + "'", "'Title'").Replace("'" + MessageKey + "'", "'Message'")));
        warnings.AddRange(PersonalDataKeys.Warnings(settings.Data.Select(r => r.Key).Where(k => !string.IsNullOrWhiteSpace(k))));

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
    public Task<IReadOnlyList<string>> ValidateSettingsAsync(object? settings, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(settings is PostMessageSettings { Message: { } m } && !string.IsNullOrWhiteSpace(m) ? [] : ["Message is required."]);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ValidateSettingsForPublishAsync(object? settings, Automation automation, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var services = scope.ServiceProvider;
        return await ConnectionChoiceValidator.ValidateAsync(
            automation,
            services.GetRequiredService<IWorkspaceService>(),
            services.GetRequiredService<IConnectionService>(),
            cancellationToken);
    }

    // Caps every value and builds the fitter's data. Long or blank values are fixed here, never failed (F01).
    private static (Dictionary<string, string> Values, List<string> Warnings) Collect(PostMessageSettings settings, int maxLength)
    {
        var warnings = new List<string>();
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TitleKey] = Cap(settings.Title, maxLength, "Title", warnings),
            [MessageKey] = Cap(settings.Message, maxLength, "Message", warnings),
        };

        if (string.IsNullOrWhiteSpace(values[MessageKey]))
        {
            values[MessageKey] = BlankMessage;
            warnings.Add("The message was empty, so a placeholder was sent.");
        }

        foreach (var row in settings.Data)
        {
            var key = row.Key is null ? null : ValueLimiter.ReplaceLoneSurrogates(row.Key).Trim();
            if (string.IsNullOrEmpty(key) || key is TitleKey or MessageKey)
            {
                continue;
            }

            values[key] = Cap(row.Value, maxLength, key, warnings);
        }

        return (values, warnings);
    }

    private static string Cap(string? value, int maxLength, string name, List<string> warnings)
    {
        var (capped, truncated) = ValueLimiter.Truncate(value, maxLength);
        if (truncated)
        {
            warnings.Add($"Card value '{name}' was shortened to fit.");
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
