using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Integration.Capabilities;

public interface IControlPanelCapabilitySnapshotClient
{
    Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(CancellationToken cancellationToken);
}

public sealed class ControlPanelCapabilitySnapshotClient : IControlPanelCapabilitySnapshotClient
{
    public const string ApiKeyHeaderName = "X-Yagot-Api-Key";

    private readonly HttpClient _httpClient;
    private readonly CapabilityReconciliationOptions _options;
    private readonly TimeProvider _timeProvider;

    public ControlPanelCapabilitySnapshotClient(
        HttpClient httpClient,
        IOptions<CapabilityReconciliationOptions> options,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.SnapshotHttpTimeoutSeconds));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"api/v1/sites/{_options.SiteId}/capabilities");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.CacheControl = new CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };
        request.Headers.TryAddWithoutValidation(ApiKeyHeaderName, _options.SnapshotApiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
        {
            return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Network);
        }
        catch (IOException)
        {
            return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Network);
        }

        using (response)
        {
            if (response.StatusCode != HttpStatusCode.OK)
            {
                return ClassifyNonSuccess(response);
            }

            if (response.Headers.CacheControl?.NoStore != true)
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.MissingNoStore);
            }

            if (!HasSupportedJsonMediaType(response.Content.Headers.ContentType))
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.InvalidMediaType);
            }

            if (response.Content.Headers.ContentLength is > CapabilityReconciliationOptions.ResponseSizeLimitBytes)
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.ResponseTooLarge);
            }

            try
            {
                await using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
                var bytes = await ReadBoundedAsync(body, timeout.Token);
                return ControlPanelSnapshotClientResult.Success(bytes);
            }
            catch (ResponseTooLargeException)
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.ResponseTooLarge);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeout.IsCancellationRequested)
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Timeout);
            }
            catch (HttpRequestException)
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Network);
            }
            catch (IOException)
            {
                return ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Network);
            }
        }
    }

    private ControlPanelSnapshotClientResult ClassifyNonSuccess(HttpResponseMessage response) =>
        response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Unauthorized),
            HttpStatusCode.Forbidden => ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Forbidden),
            HttpStatusCode.NotFound => ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.NotFound),
            HttpStatusCode.TooManyRequests => ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.Throttled,
                ReadRetryAfter(response.Headers.RetryAfter)),
            >= HttpStatusCode.InternalServerError => ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.ServerError,
                ReadRetryAfter(response.Headers.RetryAfter)),
            _ => ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Protocol)
        };

    private TimeSpan? ReadRetryAfter(RetryConditionHeaderValue? retryAfter)
    {
        if (retryAfter?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (retryAfter?.Date is not { } date)
        {
            return null;
        }

        var calculated = date - _timeProvider.GetUtcNow();
        return calculated < TimeSpan.Zero ? TimeSpan.Zero : calculated;
    }

    private static bool HasSupportedJsonMediaType(MediaTypeHeaderValue? value)
    {
        if (value is null) return false;
        var mediaType = value.MediaType;
        var isJson = string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                     mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true;
        if (!isJson) return false;

        var charset = value.CharSet?.Trim('"');
        return string.IsNullOrEmpty(charset) || string.Equals(charset, "utf-8", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream body, CancellationToken cancellationToken)
    {
        await using var destination = new MemoryStream(8 * 1024);
        var buffer = new byte[8 * 1024];
        var total = 0;

        while (true)
        {
            var remaining = CapabilityReconciliationOptions.ResponseSizeLimitBytes - total + 1;
            var read = await body.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken);
            if (read == 0) return destination.ToArray();

            total += read;
            if (total > CapabilityReconciliationOptions.ResponseSizeLimitBytes)
            {
                throw new ResponseTooLargeException();
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private sealed class ResponseTooLargeException : Exception;
}
