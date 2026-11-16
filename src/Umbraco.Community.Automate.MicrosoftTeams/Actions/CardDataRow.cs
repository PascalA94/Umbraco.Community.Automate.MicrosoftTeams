namespace Umbraco.Community.Automate.MicrosoftTeams.Actions;

/// <summary>One key and value row shown as a fact in the message.</summary>
public sealed class CardDataRow
{
    /// <summary>The label.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The value. Bindings are allowed.</summary>
    public string? Value { get; set; }
}
