using System.Diagnostics;

namespace Umbraco.Community.Automate.MicrosoftTeams.Tests.Support;

/// <summary>Listens to every ActivitySource and the HttpHandlerDiagnosticListener, recording tags and the ToString() of each payload.</summary>
internal sealed class TelemetryCapture : IDisposable, IObserver<DiagnosticListener>
{
    private readonly ActivityListener _activities;
    private readonly List<IDisposable> _subscriptions = [];
    private readonly IDisposable _all;

    public TelemetryCapture()
    {
        _activities = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                lock (Texts)
                {
                    foreach (var tag in a.TagObjects)
                    {
                        Texts.Add($"{tag.Key}={tag.Value}");
                    }

                    Texts.Add(a.DisplayName);
                }
            },
        };
        ActivitySource.AddActivityListener(_activities);
        _all = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public List<string> Texts { get; } = [];

    /// <summary>Payload objects handed to the diagnostic listener, by type (the request itself is expected to be among them).</summary>
    public List<object> Payloads { get; } = [];

    public void OnNext(DiagnosticListener listener)
    {
        if (listener.Name == "HttpHandlerDiagnosticListener")
        {
            _subscriptions.Add(listener.Subscribe(new Observer(this)));
        }
    }

    public void OnCompleted()
    {
    }

    public void OnError(Exception error)
    {
    }

    public void Dispose()
    {
        _activities.Dispose();
        _all.Dispose();
        foreach (var s in _subscriptions)
        {
            s.Dispose();
        }
    }

    private sealed class Observer(TelemetryCapture owner) : IObserver<KeyValuePair<string, object?>>
    {
        public void OnNext(KeyValuePair<string, object?> value)
        {
            lock (owner.Texts)
            {
                owner.Texts.Add($"{value.Key}={value.Value}");
                if (value.Value is not null)
                {
                    owner.Payloads.Add(value.Value);
                }
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }
}
