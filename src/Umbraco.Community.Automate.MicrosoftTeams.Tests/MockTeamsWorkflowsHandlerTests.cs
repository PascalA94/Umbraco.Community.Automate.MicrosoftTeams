using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Configuration;
using Umbraco.Community.Automate.MicrosoftTeams.Mock;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests;

public class MockTeamsWorkflowsHandlerTests
{
    private const string Url = "https://mock.workflows.invalid/hook";

    private static readonly string ValidEnvelope = """
        {"type":"message","attachments":[{"contentType":"application/vnd.microsoft.card.adaptive","contentUrl":null,"content":{"type":"AdaptiveCard","version":"1.4","body":[]}}]}
        """;

    private static async Task<HttpResponseMessage> Send(MockTeamsWorkflowsHandler handler, string body, HttpMethod? method = null, string contentType = "application/json")
    {
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, Url)
        {
            Content = new StringContent(body, Encoding.UTF8, contentType),
        };
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Mock_ValidEnvelope_Returns202EmptyBody()
    {
        var handler = new MockTeamsWorkflowsHandler();
        using var response = await Send(handler, ValidEnvelope);
        Assert.Equal((HttpStatusCode)TeamsWorkflowsFacts.AcceptedStatusCode, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Single(handler.AcceptedEnvelopes);
    }

    [Fact]
    public async Task Mock_BadJson_RejectedWithFactsShape()
    {
        var handler = new MockTeamsWorkflowsHandler();
        using var response = await Send(handler, "{not json");
        Assert.Equal((HttpStatusCode)TeamsWorkflowsFacts.BadJsonStatusCode, response.StatusCode);
        Assert.Equal(TeamsWorkflowsFacts.BadJsonBody, await response.Content.ReadAsStringAsync());
        Assert.Empty(handler.AcceptedEnvelopes);
    }

    [Fact]
    public async Task Mock_OverSizeLimit_RejectedWithFactsShape()
    {
        var handler = new MockTeamsWorkflowsHandler();
        var big = ValidEnvelope.Replace("\"body\":[]", "\"body\":[{\"type\":\"TextBlock\",\"text\":\"" + new string('x', TeamsWorkflowsFacts.MaxPayloadBytes) + "\"}]");
        using var response = await Send(handler, big);
        Assert.Equal((HttpStatusCode)TeamsWorkflowsFacts.OversizeStatusCode, response.StatusCode);
        Assert.Equal(TeamsWorkflowsFacts.OversizeBody, await response.Content.ReadAsStringAsync());
        Assert.Empty(handler.AcceptedEnvelopes);
    }

    [Fact]
    public async Task Mock_NotPost_Or_NotJson_Rejected()
    {
        var handler = new MockTeamsWorkflowsHandler();
        using var get = await Send(handler, ValidEnvelope, HttpMethod.Get);
        using var text = await Send(handler, ValidEnvelope, contentType: "text/plain");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, text.StatusCode);
        Assert.Empty(handler.AcceptedEnvelopes);
    }

    [Fact]
    public async Task Mock_ModeThrottled_Returns429WithRetryAfter()
    {
        var handler = new MockTeamsWorkflowsHandler { Mode = MockMode.Throttled };
        using var response = await Send(handler, ValidEnvelope);
        Assert.Equal((HttpStatusCode)TeamsWorkflowsFacts.ThrottleStatusCode, response.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(TeamsWorkflowsFacts.ThrottleRetryAfterSeconds), response.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Mock_ModeWorkflowDeleted_ReturnsFactsShape()
    {
        var handler = new MockTeamsWorkflowsHandler { Mode = MockMode.WorkflowDeleted };
        using var response = await Send(handler, ValidEnvelope);
        Assert.Equal((HttpStatusCode)TeamsWorkflowsFacts.WorkflowGoneStatusCode, response.StatusCode);
        Assert.Equal(TeamsWorkflowsFacts.WorkflowGoneBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mock_ModeHang_NeverCompletesUntilCancelled()
    {
        var handler = new MockTeamsWorkflowsHandler { Mode = MockMode.Hang };
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await client.PostAsync(Url, new StringContent(ValidEnvelope, Encoding.UTF8, "application/json"), cts.Token));
    }

    [Fact]
    public void TeamsOptions_DefaultsComeFromFacts()
    {
        var options = new ConfigurationBuilder().Build().GetSection(TeamsOptions.SectionName).Get<TeamsOptions>() ?? new TeamsOptions();
        new TeamsOptionsPostConfigure().PostConfigure(null, options);
        Assert.Equal(TeamsWorkflowsFacts.DefaultAllowedHosts, options.AllowedHosts);
        Assert.Equal(TeamsWorkflowsFacts.MaxPayloadBytes, options.MaxPayloadBytes);
        Assert.Equal(TeamsWorkflowsFacts.DefaultCardVersion, options.DefaultCardVersion);
        Assert.Equal(TeamsWorkflowsFacts.CardVersions, options.CardVersions);
        Assert.Equal(TeamsWorkflowsFacts.RequestsPerSecond, options.RateLimit.RequestsPerSecond);
        Assert.Equal(TeamsWorkflowsFacts.Burst, options.RateLimit.Burst);
    }
}
