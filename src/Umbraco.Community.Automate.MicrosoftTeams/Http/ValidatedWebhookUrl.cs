namespace Umbraco.Community.Automate.MicrosoftTeams.Http;

/// <summary>A webhook URL that passed the checks. It never prints itself: the host can carry a secret.</summary>
internal sealed class ValidatedWebhookUrl
{
    internal ValidatedWebhookUrl(Uri uri, string host)
    {
        Uri = uri;
        Host = host;
    }

    internal Uri Uri { get; }

    internal string Host { get; }

    /// <summary>A constant, so passing this to a logger by mistake leaks nothing.</summary>
    public override string ToString() => "[webhook URL]";
}
