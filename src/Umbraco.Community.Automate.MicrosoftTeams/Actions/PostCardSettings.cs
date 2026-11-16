using Umbraco.Automate.Core.Settings;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>Settings for <c>community.teams.postCard</c>. Property names are permanent once released.</summary>
public sealed class PostCardSettings
{
    /// <summary>Which card to send.</summary>
    [Field(Label = "Card", Description = "A built-in card, or your own card JSON.", SupportsBindings = false, SortOrder = 1)]
    public CardSource CardSource { get; set; } = CardSource.Simple;

    /// <summary>The author's card. Bindings are off on purpose: data goes in Card data and is referenced with <c>{{key}}</c>.</summary>
    [Field(
        Label = "Custom card JSON",
        Description = "An Adaptive Card, or a full message with attachments. Use {{key}} for values from Card data.",
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        SupportsBindings = false,
        VisibleWhen = nameof(CardSource),
        VisibleWhenValues = [nameof(Cards.CardSource.Custom)],
        SortOrder = 2)]
    public string? CustomCardJson { get; set; }

    /// <summary>Key and value rows the card's <c>{{key}}</c> placeholders are filled from.</summary>
    [Field(Label = "Card data", Description = "Values are escaped and shown in the run log to workspace members.", EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.KeyValueEditor", SupportsBindings = true, SortOrder = 3)]
    public List<CardDataRow> CardData { get; set; } = [];

    /// <summary>Notification preview text.</summary>
    [Field(Label = "Summary", Description = "The preview text in the notification.", SupportsBindings = true, SortOrder = 4)]
    public string? Summary { get; set; }

    /// <summary>Shows Card data as typed instead of reading it as Markdown.</summary>
    [Field(Label = "Escape Markdown in data", Description = "Show data as typed, and stop pasted links from being clickable.", SupportsBindings = false, Group = "Advanced", SortOrder = 5)]
    public bool EscapeMarkdownInData { get; set; } = true;

    /// <summary>Lets a custom card's links point outside this site. Ignored for built-in cards.</summary>
    [Field(
        Label = "Allow external links",
        Description = "Allow links to sites other than this one.",
        SupportsBindings = false,
        Group = "Advanced",
        VisibleWhen = nameof(CardSource),
        VisibleWhenValues = [nameof(Cards.CardSource.Custom)],
        SortOrder = 6)]
    public bool AllowExternalLinks { get; set; }
}
