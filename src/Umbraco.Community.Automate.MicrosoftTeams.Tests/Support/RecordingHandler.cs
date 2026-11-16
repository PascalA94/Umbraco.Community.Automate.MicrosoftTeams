using System.Net;
using System.Text;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>A fake HTTP primary handler: answers a set status and body, throws, or never completes. Records every request.</summary>
internal sealed class RecordingHandler : HttpMessageHandler
{
    public List<(string Uri, string? ContentType, string Body)> Requests { get; } = [];

    public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;

    public string Body { get; set; } = string.Empty;

    public Stream? BodyStream { get; set; }

    public Exception? Throw { get; set; }

    public bool Hang { get; set; }

    public Dictionary<string, string> Headers { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.RequestUri!.AbsoluteUri, request.Content?.Headers.ContentType?.ToString(), body));
        if (Throw is not null)
        {
            throw Throw;
        }

        if (Hang)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        var response = new HttpResponseMessage(Status)
        {
            Content = BodyStream is not null ? new StreamContent(BodyStream) : new StringContent(Body, Encoding.UTF8),
        };
        foreach (var (k, v) in Headers)
        {
            response.Headers.TryAddWithoutValidation(k, v);
        }

        return response;
    }
}

/// <summary>An <see cref="IHttpClientFactory"/> that hands out clients over one handler and remembers the names asked for.</summary>
internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return new HttpClient(handler, disposeHandler: false);
    }
}
