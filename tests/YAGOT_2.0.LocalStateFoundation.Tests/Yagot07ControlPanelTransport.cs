using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

internal sealed class Yagot07ControlPanelTransport : HttpMessageHandler
{
    private readonly object _gate = new();
    private SiteStateSnapshotV1? _snapshot;
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private bool _includeNoStore = true;
    private string _mediaType = "application/json";
    private int _callCount;
    private TaskCompletionSource? _nextEntered;
    private TaskCompletionSource? _nextRelease;

    public int CallCount => Volatile.Read(ref _callCount);
    public HttpRequestMessage? LastRequest { get; private set; }

    public void SetSnapshot(SiteStateSnapshotV1 snapshot)
    {
        lock (_gate)
        {
            _snapshot = snapshot;
            _statusCode = HttpStatusCode.OK;
            _includeNoStore = true;
            _mediaType = "application/json";
        }
    }

    public void SetFailure(HttpStatusCode statusCode)
    {
        lock (_gate)
        {
            _statusCode = statusCode;
        }
    }

    public void SetInvalidBody(string mediaType = "application/json")
    {
        lock (_gate)
        {
            _snapshot = null;
            _statusCode = HttpStatusCode.OK;
            _includeNoStore = true;
            _mediaType = mediaType;
        }
    }

    public (Task Entered, Action Release) DelayNextResponse()
    {
        lock (_gate)
        {
            var entered = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _nextEntered = entered;
            _nextRelease = release;
            return (entered.Task, () => release.TrySetResult());
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);
        LastRequest = CloneRequest(request);

        SiteStateSnapshotV1? snapshot;
        HttpStatusCode statusCode;
        bool includeNoStore;
        string mediaType;
        Task? release;
        lock (_gate)
        {
            snapshot = _snapshot;
            statusCode = _statusCode;
            includeNoStore = _includeNoStore;
            mediaType = _mediaType;
            _nextEntered?.TrySetResult();
            release = _nextRelease?.Task;
            _nextEntered = null;
            _nextRelease = null;
        }

        if (release is not null)
        {
            await release.WaitAsync(cancellationToken);
        }

        var response = new HttpResponseMessage(statusCode);
        if (statusCode == HttpStatusCode.OK)
        {
            var body = snapshot is null
                ? "{malformed"
                : JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            response.Content = new StringContent(body);
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(mediaType);
            if (includeNoStore)
            {
                response.Headers.CacheControl = new CacheControlHeaderValue { NoStore = true };
            }
        }

        return response;
    }

    protected override void Dispose(bool disposing)
    {
        // The test fixture owns this handler and may use it across typed-client lifetimes.
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        return clone;
    }
}
