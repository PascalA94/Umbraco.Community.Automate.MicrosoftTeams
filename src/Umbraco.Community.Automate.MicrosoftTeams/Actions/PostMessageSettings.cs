using Umbraco.Automate.Core.Settings;

namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>How the message text is read.</summary>
public enum MessageFormatting
{
    /// <summary>Shown as typed (the default, so bound data cannot inject formatting).</summary>
    PlainText,

    /// <summary>Markdown in the title and message is interpreted.</summary>
    Markdown,
}

/// <summary>The accent of the message card.</summary>
public enum MessageStyle
{
    /// <summary>No accent.</summary>
    Default,

    /// <summary>Success.</summary>
    Good,

    /// <summary>Needs a look.</summary>
    Warning,

    /// <summary>Needs action now.</summary>
    Attention,
}

/// <summary>Settings for <c>community.teams.postMessage</c>. Property names are permanent once released.</summary>
public sealed class PostMessageSettings
{
    /// <summary>The card title.</summary>
    [Field(Label = "Title", Description = "A {{key}} here is filled from Data, including one that arrives in bound text.", SupportsBindings = true, SortOrder = 1)]
    public string? Title { get; set; }

    /// <summary>The message text.</summary>
    [Field(Label = "Message", Description = "A {{key}} here is filled from Data, including one that arrives in bound text.", SupportsBindings = true, SortOrder = 2)]
    public string? Message { get; set; }

    /// <summary>Extra key and value rows.</summary>
    [Field(Label = "Data", Description = "Values are escaped and shown in the run log to workspace members.", EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.KeyValueEditor", SupportsBindings = true, SortOrder = 3)]
    public List<CardDataRow> Data { get; set; } = [];

    /// <summary>An optional button link.</summary>
    [Field(Label = "Link URL", SupportsBindings = true, SortOrder = 4)]
    public string? LinkUrl { get; set; }

    /// <summary>The button label. Name the destination, never "Click here".</summary>
    [Field(Label = "Link label", SupportsBindings = true, SortOrder = 5)]
    public string? LinkLabel { get; set; }

    /// <summary>The card accent.</summary>
    [Field(Label = "Style", SupportsBindings = false, SortOrder = 6)]
    public MessageStyle Style { get; set; }

    /// <summary>How the text is read. No bindings: a run must not be able to switch Markdown on.</summary>
    [Field(Label = "Formatting", Description = "Bound data in Title and Message isn't escaped. Put untrusted values in Data instead.", SupportsBindings = false, SortOrder = 7)]
    public MessageFormatting Formatting { get; set; } = MessageFormatting.PlainText;

    /// <summary>Lets the button point outside this site.</summary>
    [Field(Label = "Allow external links", Description = "Allow the button to link to sites other than this one.", SupportsBindings = false, Group = "Advanced", SortOrder = 8)]
    public bool AllowExternalLinks { get; set; }
}
