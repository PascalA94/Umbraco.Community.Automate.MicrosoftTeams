using Umbraco.Automate.Core.Actions;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>A failure with the category Automate shows and a message we wrote. The message never holds the URL.</summary>
internal sealed record TeamsFailure(StepRunErrorCategory Category, string Message);

/// <summary>Maps a Teams status to a failure (design 7.5). Messages are fixed text plus, for 400 only, a scrubbed snippet.</summary>
internal static class TeamsErrorClassifier
{
    public static TeamsFailure? Classify(int statusCode, string? scrubbedBody)
    {
        if (statusCode is >= 200 and < 300)
        {
            return null;
        }

        return statusCode switch
        {
            >= 300 and < 400 => new(StepRunErrorCategory.InvalidResponse, "Teams answered with a redirect. Check the webhook URL."),
            400 => new(StepRunErrorCategory.InvalidResponse, string.IsNullOrWhiteSpace(scrubbedBody) ? "Teams rejected the card (400)." : $"Teams rejected the card (400). {scrubbedBody}"),
            401 or 403 => new(StepRunErrorCategory.Authentication, "Teams refused the webhook signature. Re-create the Workflow URL."),
            404 => new(StepRunErrorCategory.ConfigurationError, "The workflow was deleted or turned off."),
            413 => new(StepRunErrorCategory.Validation, "Teams says the message is too large."),
            429 => new(StepRunErrorCategory.RateLimiting, "Teams is throttling this webhook. Use the step's Retry setting."),
            >= 500 => new(StepRunErrorCategory.ServiceUnavailable, $"Teams returned {statusCode}. Try again later."),

            // Any other non-2xx: an unmapped answer is still an invalid response (architect's call).
            _ => new(StepRunErrorCategory.InvalidResponse, $"Teams returned {statusCode}."),
        };
    }
}
