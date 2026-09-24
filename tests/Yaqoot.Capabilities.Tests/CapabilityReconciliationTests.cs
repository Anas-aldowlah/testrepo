using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class CapabilityReconciliationTests
{
    private readonly CapabilityCatalog _catalog = new();
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);
    private readonly TimeProvider _time = TimeProvider.System;

    [Fact]
    public async Task SnapshotClient_SendsCorrectHeadersAndUri_AndParsesResponse()
    {
        HttpRequestMessage? capturedRequest = null;
        var validSnapshot = CreateSnapshot();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(validSnapshot, _json));

        var handler = new MockHttpMessageHandler(async req =>
        {
            capturedRequest = req;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(jsonBytes)
            };
            response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            return await Task.FromResult(response);
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://controlpanel.test/")
        };

        var options = Options.Create(new CapabilityReconciliationOptions
        {
            ControlPanelBaseUrl = "https://controlpanel.test/",
            SiteId = 1,
            SnapshotApiKey = "test-secret-key"
        });

        var client = new ControlPanelCapabilitySnapshotClient(
            httpClient,
            options,
            _time);

        var result = await client.GetSnapshotAsync(CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Body);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://controlpanel.test/api/v1/sites/1/capabilities", capturedRequest!.RequestUri!.ToString());
        Assert.True(capturedRequest.Headers.TryGetValues("X-Yagot-Api-Key", out var keyValues));
        Assert.Equal("test-secret-key", keyValues.Single());
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ControlPanelSnapshotFailure.Unauthorized, false)]
    [InlineData(HttpStatusCode.Forbidden, ControlPanelSnapshotFailure.Forbidden, false)]
    [InlineData(HttpStatusCode.NotFound, ControlPanelSnapshotFailure.NotFound, false)]
    [InlineData(HttpStatusCode.TooManyRequests, ControlPanelSnapshotFailure.Throttled, true)]
    [InlineData(HttpStatusCode.InternalServerError, ControlPanelSnapshotFailure.ServerError, true)]
    [InlineData(HttpStatusCode.BadGateway, ControlPanelSnapshotFailure.ServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, ControlPanelSnapshotFailure.ServerError, true)]
    public async Task SnapshotClient_ClassifiesHttpErrorsCorrectly(
        HttpStatusCode statusCode,
        ControlPanelSnapshotFailure expectedFailure,
        bool expectedRetryable)
    {
        var handler = new MockHttpMessageHandler(async _ =>
        {
            var res = new HttpResponseMessage(statusCode);
            if (statusCode is HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests)
            {
                res.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
            }
            return await Task.FromResult(res);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://controlpanel.test/") };
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1, SnapshotApiKey = "k" });
        var client = new ControlPanelCapabilitySnapshotClient(
            httpClient,
            options,
            _time);

        var result = await client.GetSnapshotAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedFailure, result.Failure);
        Assert.Equal(expectedRetryable, result.IsRetryable);
        if (statusCode == HttpStatusCode.ServiceUnavailable)
        {
            Assert.Equal(TimeSpan.FromSeconds(5), result.RetryAfter);
        }
    }

    [Fact]
    public async Task SnapshotClient_RejectsOversizedPayload()
    {
        var oversized = new byte[65 * 1024]; // 65 KB exceeds 64 KB cap
        Array.Fill(oversized, (byte)'A');

        var handler = new MockHttpMessageHandler(async _ =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(oversized)
            };
            res.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true };
            res.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            return await Task.FromResult(res);
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://controlpanel.test/") };
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1, SnapshotApiKey = "k" });
        var client = new ControlPanelCapabilitySnapshotClient(
            httpClient,
            options,
            _time);

        var result = await client.GetSnapshotAsync(CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(ControlPanelSnapshotFailure.ResponseTooLarge, result.Failure);
    }

    [Fact]
    public async Task ReconciliationService_ReconcilesSuccessfully_WhenSnapshotValid()
    {
        var snapshot = CreateSnapshot();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, _json));

        var mockClient = new StubSnapshotClient(ControlPanelSnapshotClientResult.Success(jsonBytes));
        var parser = new CapabilitySnapshotV1JsonParser(_catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 }));
        var mockApply = new StubApplyService(new CapabilityApplyResult(CapabilityApplyOutcome.Applied, CapabilityHealthStatus.Healthy));
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1 });

        var service = new CapabilityReconciliationService(
            mockClient,
            parser,
            mockApply,
            options,
            _time,
            NullLogger<CapabilityReconciliationService>.Instance);

        var result = await service.ReconcileAsync(CapabilityReconciliationReason.Startup);

        Assert.True(result.Succeeded);
        Assert.Equal("Applied", result.Outcome);
        Assert.Equal(1, result.Revision);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(1, mockApply.Calls);
    }

    [Fact]
    public async Task ReconciliationService_TreatsEqualOutcome_AsSuccess()
    {
        var snapshot = CreateSnapshot();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, _json));

        var mockClient = new StubSnapshotClient(ControlPanelSnapshotClientResult.Success(jsonBytes));
        var parser = new CapabilitySnapshotV1JsonParser(_catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 }));
        var mockApply = new StubApplyService(new CapabilityApplyResult(CapabilityApplyOutcome.Equal, CapabilityHealthStatus.Healthy));
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1 });

        var service = new CapabilityReconciliationService(
            mockClient,
            parser,
            mockApply,
            options,
            _time,
            NullLogger<CapabilityReconciliationService>.Instance);

        var result = await service.ReconcileAsync(CapabilityReconciliationReason.Periodic);

        Assert.True(result.Succeeded);
        Assert.Equal("Equal", result.Outcome);
        Assert.Equal(1, result.Revision);
    }

    [Fact]
    public async Task ReconciliationService_ReportsFailure_WhenClientFailsNonRetryable()
    {
        var mockClient = new StubSnapshotClient(ControlPanelSnapshotClientResult.Failed(ControlPanelSnapshotFailure.Unauthorized));
        var parser = new CapabilitySnapshotV1JsonParser(_catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 }));
        var mockApply = new StubApplyService(new CapabilityApplyResult(CapabilityApplyOutcome.Applied, CapabilityHealthStatus.Healthy));
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1 });

        var service = new CapabilityReconciliationService(
            mockClient,
            parser,
            mockApply,
            options,
            _time,
            NullLogger<CapabilityReconciliationService>.Instance);

        var result = await service.ReconcileAsync(CapabilityReconciliationReason.Startup);

        Assert.False(result.Succeeded);
        Assert.Equal("Unauthorized", result.FailureCode);
        Assert.Equal(1, result.Attempts);
        Assert.Equal(0, mockApply.Calls);
    }

    [Fact]
    public async Task ReconciliationService_RetriesTransientFailure_AndSucceeds()
    {
        var snapshot = CreateSnapshot();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, _json));

        var mockClient = new FlakySnapshotClient(
            failCount: 2,
            successResult: ControlPanelSnapshotClientResult.Success(jsonBytes));

        var parser = new CapabilitySnapshotV1JsonParser(_catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 }));
        var mockApply = new StubApplyService(new CapabilityApplyResult(CapabilityApplyOutcome.Applied, CapabilityHealthStatus.Healthy));
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1 });

        var service = new CapabilityReconciliationService(
            mockClient,
            parser,
            mockApply,
            options,
            _time,
            NullLogger<CapabilityReconciliationService>.Instance);

        var result = await service.ReconcileAsync(CapabilityReconciliationReason.Startup);

        Assert.True(result.Succeeded);
        Assert.Equal("Applied", result.Outcome);
        Assert.Equal(3, result.Attempts);
        Assert.Equal(1, mockApply.Calls);
    }

    [Fact]
    public async Task ReconciliationService_PreventsConcurrentReconciliation()
    {
        var tcs = new TaskCompletionSource<ControlPanelSnapshotClientResult>();
        var blockClient = new BlockingSnapshotClient(tcs.Task);
        var parser = new CapabilitySnapshotV1JsonParser(_catalog, Options.Create(new CapabilityIntegrationOptions { SiteId = 1 }));
        var mockApply = new StubApplyService(new CapabilityApplyResult(CapabilityApplyOutcome.Applied, CapabilityHealthStatus.Healthy));
        var options = Options.Create(new CapabilityReconciliationOptions { SiteId = 1 });

        var service = new CapabilityReconciliationService(
            blockClient,
            parser,
            mockApply,
            options,
            _time,
            NullLogger<CapabilityReconciliationService>.Instance);

        var firstTask = service.ReconcileAsync(CapabilityReconciliationReason.Startup);
        var secondResult = await service.ReconcileAsync(CapabilityReconciliationReason.Periodic);

        Assert.True(secondResult.Succeeded);
        Assert.Equal("AlreadyRunning", secondResult.Outcome);

        // Complete the first task
        var snapshot = CreateSnapshot();
        var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(snapshot, _json));
        tcs.SetResult(ControlPanelSnapshotClientResult.Success(jsonBytes));

        var firstResult = await firstTask;
        Assert.True(firstResult.Succeeded);
        Assert.Equal("Applied", firstResult.Outcome);
    }

    private CapabilitySnapshotV1 CreateSnapshot()
    {
        var at = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        return new(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            1,
            at,
            at,
            _catalog.Modules.Select(x => new CapabilityModuleStateV1(x.Code, true)).ToArray(),
            _catalog.Features.Select(x => new CapabilityFeatureStateV1(x.Code, x.ModuleCode, true)).ToArray());
    }

    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handlerFunc) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handlerFunc(request);
    }

    private sealed class StubSnapshotClient(ControlPanelSnapshotClientResult result) : IControlPanelCapabilitySnapshotClient
    {
        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(result);
    }

    private sealed class FlakySnapshotClient(int failCount, ControlPanelSnapshotClientResult successResult) : IControlPanelCapabilitySnapshotClient
    {
        private int _calls;

        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            _calls++;
            if (_calls <= failCount)
            {
                return Task.FromResult(ControlPanelSnapshotClientResult.Failed(
                    ControlPanelSnapshotFailure.ServerError,
                    retryAfter: TimeSpan.FromMilliseconds(10)));
            }

            return Task.FromResult(successResult);
        }
    }

    private sealed class BlockingSnapshotClient(Task<ControlPanelSnapshotClientResult> pendingTask) : IControlPanelCapabilitySnapshotClient
    {
        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => pendingTask;
    }

    private sealed class StubApplyService(CapabilityApplyResult result) : ICapabilityApplyService
    {
        public int Calls { get; private set; }
        public CapabilitySnapshotV1? Snapshot { get; private set; }

        public Task<CapabilityApplyResult> ApplyAsync(
            CapabilitySnapshotV1 snapshot,
            CapabilityDeliveryContext? delivery = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Snapshot = snapshot;
            return Task.FromResult(result);
        }
    }

    [Theory]
    [InlineData(404, false)]
    [InlineData(403, true)]
    public void NotFoundPage_RendersExpectedViewAndStatusCode(int statusCode, bool expectedIsForbidden)
    {
        var controller = new Yagot.Controllers.HomeController(
            null!, null!, null!, null!, null!, null!, null!, null!)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            }
        };

        var result = controller.NotFoundPage(statusCode) as Microsoft.AspNetCore.Mvc.ViewResult;

        Assert.NotNull(result);
        Assert.Equal("NotFound", result.ViewName);
        Assert.Equal(statusCode, controller.Response.StatusCode);
        Assert.Equal(expectedIsForbidden, controller.ViewBag.IsFeatureForbidden);
    }
}
