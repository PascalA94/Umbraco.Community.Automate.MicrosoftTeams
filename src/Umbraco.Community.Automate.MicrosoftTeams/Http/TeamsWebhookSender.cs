using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Community.Automate.MicrosoftTeams.Cards;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Credentials;

namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>
/// The one code path that touches the webhook URL: credential lookup, URL checks, the rate limiter, the size check, the
/// POST and error mapping (design 7.2). Every message it returns is written here; nothing from an exception or a
/// response body reaches a caller except a scrubbed snippet of a 400.
/// </summary>
internal sealed class TeamsWebhookSender(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<TeamsOptions> options,
    TeamsCredentialStore store,
    WebhookUrlValidator validator,
    WebhookRateLimiter limiter,
    TimeProvider timeProvider,
    ILogger<TeamsWebhookSender> logger) : ITeamsWebhookSender
{
    /// <summary>
    /// Automate's own named client: no redirects, and the SSRF protection handler as its primary handler (that handler is
    /// internal to Automate, so the package can't build a client of its own). Tests and the demo mock replace this
    /// client's primary handler only, which keeps this class's checks in play.
    /// </summary>
    public const string HttpClientName = Umbraco.Automate.Core.Constants.HttpClients.NoRedirect;

    private const int MaxErrorBodyBytes = 1024;

    // There is no save hook (spike S3), so a URL typed into the name field is already stored in plaintext by the time it
    // is seen here. The message says so without echoing a single character of it.
    internal const string PastedUrlMessage = "This looks like a webhook URL. It may have been saved on the connection: clear the field and rotate the Workflow URL (see SECURITY.md#rotation).";

    // Longer than any valid name (32) is treated as a paste too: a signature or path pasted without its scheme is as sensitive.
    private static bool LooksLikePastedUrl(string? name) =>
        name is { Length: > 0 }
        && (name.Length > 32
            || name.Contains("://", StringComparison.Ordinal)
            || name.Contains("sig=", StringComparison.OrdinalIgnoreCase)
            || name.Contains("sig%3d", StringComparison.OrdinalIgnoreCase));

    public async Task<TeamsPostResult> PostAsync(string? credentialName, JsonObject envelope, CancellationToken cancellationToken)
    {
        // The store runs the allow-list guard, the name rule and the read. Its error texts are the contract's.
        if (!store.TryGet(credentialName, TeamsCredentialPart.WebhookUrl, out var url, out var error))
        {
            if (LooksLikePastedUrl(credentialName) && !store.TryNormaliseName(credentialName, out _, out _))
            {
                return Failed(StepRunErrorCategory.ConfigurationError, PastedUrlMessage);
            }

            return Failed(StepRunErrorCategory.ConfigurationError, error ?? "No credential is configured.");
        }

        store.TryNormaliseName(credentialName, out var name, out _);

        var check = validator.Check(url);
        var warnings = check.Warning is null ? [] : new List<string> { check.Warning };
        var testWarnings = check.TestWarning is null ? [] : new List<string> { check.TestWarning };
        if (check.IsBlocked)
        {
            return Failed(StepRunErrorCategory.ConfigurationError, check.Error!, warnings, testWarnings);
        }

        var current = options.CurrentValue;
        var (json, bytes) = CardEnvelope.Serialize(envelope);
        if (bytes > current.MaxPayloadBytes)
        {
            // Defence in depth: the fitter keeps data-driven size problems from ever reaching here.
            return Failed(StepRunErrorCategory.Validation, $"Card is {bytes} bytes. Teams allows about {current.MaxPayloadBytes / 1000} KB.", warnings, testWarnings, bytes);
        }

        var timeout = TimeSpan.FromSeconds(current.TimeoutSeconds);
        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            var maxWait = TimeSpan.FromSeconds(Math.Min(current.RateLimit.MaxWaitSeconds, current.TimeoutSeconds));
            if (!await limiter.WaitAsync(name!, maxWait, linked.Token))
            {
                return Failed(StepRunErrorCategory.RateLimiting, "Too many posts to this Teams webhook at once. Use the step's Retry setting.", warnings, testWarnings, bytes);
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, check.Url!.Uri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            using var response = await httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token);

            var status = (int)response.StatusCode;
            logger.LogInformation("Teams webhook returned {StatusCode} ({Bytes} bytes)", status, bytes);

            if (status is >= 200 and < 300)
            {
                return new TeamsPostResult(true, status, bytes, warnings, testWarnings, null);
            }

            var body = await ReadSnippetAsync(response, linked.Token);
            var failure = TeamsErrorClassifier.Classify(status, SecretScrubber.Scrub(body, hostPatterns: current.AllowedHosts, knownHost: check.Url.Host));
            return new TeamsPostResult(false, status, bytes, warnings, testWarnings, failure);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Host shutdown or the caller giving up: not ours to turn into a failure.
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failed(StepRunErrorCategory.Timeout, $"Teams didn't answer within {current.TimeoutSeconds} seconds.", warnings, testWarnings, bytes);
        }
        catch (SsrfException)
        {
            return Failed(StepRunErrorCategory.ConfigurationError, "The webhook host isn't allowed.", warnings, testWarnings, bytes);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SsrfException)
        {
            // Only the inner type is read. The message can carry the URL.
            return Failed(StepRunErrorCategory.ConfigurationError, "The webhook host isn't allowed.", warnings, testWarnings, bytes);
        }
        catch (Exception)
        {
            // Never touch the exception's message, request URI or inner exception: any of them can hold the URL.
            return Failed(StepRunErrorCategory.ServiceUnavailable, "Couldn't reach Teams.", warnings, testWarnings, bytes);
        }
    }

    // Reads at most 1,024 bytes, never more, so a huge error body costs nothing.
    private static async Task<string> ReadSnippetAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaxErrorBodyBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static TeamsPostResult Failed(
        StepRunErrorCategory category,
        string message,
        IReadOnlyList<string>? warnings = null,
        IReadOnlyList<string>? testWarnings = null,
        int bytes = 0) =>
        new(false, 0, bytes, warnings ?? [], testWarnings ?? [], new TeamsFailure(category, message));
}
