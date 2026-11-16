using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;

namespace Umbraco.Community.Automate.MicrosoftTeams.Mock;

/// <summary>What the mock does with the next request.</summary>
public enum MockMode
{
    /// <summary>Check the envelope and accept it.</summary>
    Normal,

    /// <summary>Answer 429 with a Retry-After.</summary>
    Throttled,

    /// <summary>Answer as for a deleted or turned-off workflow.</summary>
    WorkflowDeleted,

    /// <summary>Answer 500.</summary>
    ServerError,

    /// <summary>Never answer until the request is cancelled.</summary>
    Hang,
}

/// <summary>
/// A fake Teams Workflows endpoint, used as the primary handler of the package's named client in tests and in the
/// demo site's Development mode. Every behaviour comes from <see cref="TeamsWorkflowsFacts"/> (all unverified until the
/// Live gate). It only ever sees made-up URLs on .invalid hosts.
/// </summary>
public sealed class MockTeamsWorkflowsHandler : HttpMessageHandler
{
    private readonly List<string> _accepted = [];

    /// <summary>What the next request gets.</summary>
    public MockMode Mode { get; set; } = MockMode.Normal;

    /// <summary>Each accepted envelope's JSON, in order, for assertions and the demo's output folder.</summary>
    public IReadOnlyList<string> AcceptedEnvelopes
    {
        get
        {
            lock (_accepted)
            {
                return [.. _accepted];
            }
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post)
        {
            return Reply(HttpStatusCode.MethodNotAllowed, "Method Not Allowed");
        }

        if (request.Content?.Headers.ContentType?.MediaType != "application/json")
        {
            return Reply(HttpStatusCode.UnsupportedMediaType, "Unsupported Media Type");
        }

        switch (Mode)
        {
            case MockMode.Hang:
                await Task.Delay(Timeout.Infinite, cancellationToken);
                break;
            case MockMode.Throttled:
                var throttled = Reply((HttpStatusCode)TeamsWorkflowsFacts.ThrottleStatusCode, string.Empty);
                throttled.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(TeamsWorkflowsFacts.ThrottleRetryAfterSeconds));
                return throttled;
            case MockMode.WorkflowDeleted:
                return Reply((HttpStatusCode)TeamsWorkflowsFacts.WorkflowGoneStatusCode, TeamsWorkflowsFacts.WorkflowGoneBody);
            case MockMode.ServerError:
                return Reply(HttpStatusCode.InternalServerError, "Internal Server Error");
        }

        var bytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        if (bytes.Length > TeamsWorkflowsFacts.MaxPayloadBytes)
        {
            return Reply((HttpStatusCode)TeamsWorkflowsFacts.OversizeStatusCode, TeamsWorkflowsFacts.OversizeBody);
        }

        try
        {
            using var document = JsonDocument.Parse(bytes);
            if (!IsValidEnvelope(document.RootElement))
            {
                return Reply((HttpStatusCode)TeamsWorkflowsFacts.BadJsonStatusCode, TeamsWorkflowsFacts.BadJsonBody);
            }
        }
        catch (JsonException)
        {
            return Reply((HttpStatusCode)TeamsWorkflowsFacts.BadJsonStatusCode, TeamsWorkflowsFacts.BadJsonBody);
        }

        lock (_accepted)
        {
            _accepted.Add(Encoding.UTF8.GetString(bytes));
        }

        return Reply((HttpStatusCode)TeamsWorkflowsFacts.AcceptedStatusCode, string.Empty);
    }

    private static bool IsValidEnvelope(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("type", out var type) && type.GetString() == TeamsWorkflowsFacts.EnvelopeType
        && root.TryGetProperty("attachments", out var attachments) && attachments.ValueKind == JsonValueKind.Array
        && attachments.GetArrayLength() >= 1
        && attachments.EnumerateArray().All(a => a.TryGetProperty("contentType", out var ct) && ct.GetString() == TeamsWorkflowsFacts.AdaptiveCardContentType);

    private static HttpResponseMessage Reply(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };
}
