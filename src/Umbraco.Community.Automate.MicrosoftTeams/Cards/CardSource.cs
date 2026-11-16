namespace Umbraco.Community.Automate.MicrosoftTeams.Cards;

/// <summary>Where the Post Adaptive Card action gets its card. Members are permanent once released.</summary>
public enum CardSource
{
    /// <summary>"Now live: title", facts and a "View page" button.</summary>
    ContentPublished,

    /// <summary>"Approval needed: title", facts, notes and an "Open in backoffice" button.</summary>
    ApprovalNeeded,

    /// <summary>"New entry: form", with no field values by design.</summary>
    FormSubmitted,

    /// <summary>A heading, text and an optional button.</summary>
    Simple,

    /// <summary>The author's own card JSON.</summary>
    Custom,
}
