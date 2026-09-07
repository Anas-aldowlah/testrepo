using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationServiceTests
{
    [Theory]
    [InlineData(SiteStateApplyOutcome.Applied, true, null)]
    [InlineData(SiteStateApplyOutcome.Equal, true, null)]
    [InlineData(SiteStateApplyOutcome.Stale, false, "remote_revision_older")]
    [InlineData(SiteStateApplyOutcome.EqualConflict, false, "equal_revision_conflict")]
    public async Task ValidSnapshot_UsesExistingApplyServiceWithoutDelivery(
        SiteStateApplyOutcome applyOutcome,
        bool expectedSuccess,
        string? expectedFailureCode)
    {
        var snapshot = SiteStateContractTests.ValidSnapshot() with { Revision = 8 };
        var apply = new CapturingApplyService(applyOutcome);
        var diagnostics = new CapturingDiagnosticsStore();
        var service = CreateService(
            new QueueSnapshotClient(Success(snapshot)),
            apply,
            diagnostics);

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.Equal(expectedSuccess, result.Succeeded);
        Assert.Equal(expectedFailureCode, result.FailureCode);
        Assert.Equal(snapshot, apply.Snapshot);
        Assert.Null(apply.Delivery);
        Assert.Equal(1, apply.CallCount);
        Assert.Equal(8, diagnostics.LastObservedRevision);
        if (expectedSuccess)
        {
            Assert.Equal(1, diagnostics.SuccessCount);
            Assert.Equal(0, diagnostics.FailureCount);
        }
        else
        {
            Assert.Equal(0, diagnostics.SuccessCount);
            Assert.Equal(1, diagnostics.FailureCount);
        }
    }

    [Theory]
    [InlineData("malformed", "malformed_json")]
    [InlineData("wrong-version", "invalid_contract_version")]
    [InlineData("wrong-site", "wrong_site_id")]
    [InlineData("wrong-mode", "invalid_mode")]
    [InlineData("missing-property", "invalid_contract")]
    [InlineData("unknown-property", "invalid_contract")]
    [InlineData("duplicate-property", "invalid_contract")]
    public async Task InvalidContract_IsNeverAppliedOrRetried(
        string caseName,
        string expectedCode)
    {
        var apply = new CapturingApplyService(SiteStateApplyOutcome.Applied);
        var client = new QueueSnapshotClient(SuccessBody(InvalidBody(caseName)));
        var service = CreateService(
            client,
            apply,
            new CapturingDiagnosticsStore());

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.FailureCode);
        Assert.Equal(1, client.CallCount);
        Assert.Equal(0, apply.CallCount);
    }

    [Fact]
    public async Task OperationalFailure_RetriesAtMostThreeTimes()
    {
        var client = new QueueSnapshotClient(
            ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.Network),
            ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.ServerError),
            ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.Timeout));
        var policy = new ImmediatePolicy();
        var service = CreateService(
            client,
            new CapturingApplyService(SiteStateApplyOutcome.Applied),
            new CapturingDiagnosticsStore(),
            policy);

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.HttpAttempts);
        Assert.Equal(3, client.CallCount);
        Assert.Equal([2, 3], policy.RetryAttempts);
    }

    [Theory]
    [InlineData(ControlPanelSnapshotFailure.Unauthorized)]
    [InlineData(ControlPanelSnapshotFailure.Forbidden)]
    [InlineData(ControlPanelSnapshotFailure.NotFound)]
    [InlineData(ControlPanelSnapshotFailure.Protocol)]
    [InlineData(ControlPanelSnapshotFailure.ResponseTooLarge)]
    public async Task PermanentFailure_HasNoInlineRetry(
        ControlPanelSnapshotFailure failure)
    {
        var client = new QueueSnapshotClient(
            ControlPanelSnapshotClientResult.Failed(failure));
        var service = CreateService(
            client,
            new CapturingApplyService(SiteStateApplyOutcome.Applied),
            new CapturingDiagnosticsStore());

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.HttpAttempts);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task RetryAfterOverFiveMinutes_StopsInlineRetry()
    {
        var client = new QueueSnapshotClient(
            ControlPanelSnapshotClientResult.Failed(
                ControlPanelSnapshotFailure.Throttled,
                TimeSpan.FromMinutes(6)));
        var service = CreateService(
            client,
            new CapturingApplyService(SiteStateApplyOutcome.Applied),
            new CapturingDiagnosticsStore(),
            new SiteStateReconciliationPolicy(Options.Create(
                new SiteStateReconciliationOptions
                {
                    ReconciliationIntervalMinutes = 30
                })));

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.HttpAttempts);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task ShutdownCancellation_DoesNotRecordFailure()
    {
        var diagnostics = new CapturingDiagnosticsStore();
        var service = CreateService(
            new CancellingSnapshotClient(),
            new CapturingApplyService(SiteStateApplyOutcome.Applied),
            diagnostics);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ReconcileAsync(
                SiteStateReconciliationReason.Periodic,
                cancellation.Token));
        Assert.Equal(0, diagnostics.FailureCount);
        Assert.Equal(0, diagnostics.SuccessCount);
    }

    [Fact]
    public async Task CheckpointFailure_DoesNotUndoSuccessfulApply()
    {
        var apply = new CapturingApplyService(SiteStateApplyOutcome.Applied);
        var service = CreateService(
            new QueueSnapshotClient(Success(
                SiteStateContractTests.ValidSnapshot())),
            apply,
            new ThrowingDiagnosticsStore());

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Startup,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, apply.CallCount);
    }

    [Fact]
    public async Task ApplyFailure_PreservesFailureAsDiagnosticWithoutRepull()
    {
        var client = new QueueSnapshotClient(Success(
            SiteStateContractTests.ValidSnapshot()));
        var service = CreateService(
            client,
            new ThrowingApplyService(),
            new CapturingDiagnosticsStore());

        var result = await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("apply_failure", result.FailureCode);
        Assert.Equal(1, client.CallCount);
    }

    [Fact]
    public async Task Logs_DoNotContainSecretBodyOrExceptionText()
    {
        const string secret = "snapshot-test-key";
        const string responseBody = "response-body-marker";
        const string exceptionText = "sensitive-database-detail";
        var logger = new CapturingLogger();
        var service = CreateService(
            new QueueSnapshotClient(Success(
                SiteStateContractTests.ValidSnapshot() with
                {
                    SiteName = responseBody
                })),
            new ThrowingApplyService(),
            new ThrowingDiagnosticsStore(),
            logger: logger);

        await service.ReconcileAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        var output = string.Join(Environment.NewLine, logger.Messages);
        Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        Assert.DoesNotContain(responseBody, output, StringComparison.Ordinal);
        Assert.DoesNotContain(exceptionText, output, StringComparison.Ordinal);
    }

    private static SiteStateReconciliationService CreateService(
        IControlPanelSnapshotClient client,
        ISiteStateApplyService apply,
        ISiteStateSyncDiagnosticsStore diagnostics,
        ISiteStateReconciliationPolicy? policy = null,
        ILogger<SiteStateReconciliationService>? logger = null) =>
        new(
            client,
            new SiteStateSnapshotV1JsonParser(),
            apply,
            diagnostics,
            policy ?? new ImmediatePolicy(),
            Options.Create(new SiteStateReconciliationOptions
            {
                SiteId = 1,
                ControlPanelBaseUrl = "https://control-panel.test/",
                SnapshotApiKey = "snapshot-test-key",
                ReconciliationIntervalMinutes = 30,
                SnapshotHttpTimeoutSeconds = 10
            }),
            TimeProvider.System,
            logger ?? NullLogger<SiteStateReconciliationService>.Instance);

    private static ControlPanelSnapshotClientResult Success(
        SiteStateSnapshotV1 snapshot) =>
        SuccessBody(JsonSerializer.SerializeToUtf8Bytes(
            snapshot,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static ControlPanelSnapshotClientResult SuccessBody(byte[] body) =>
        ControlPanelSnapshotClientResult.Success(body);

    private static byte[] InvalidBody(string caseName)
    {
        var valid = """
            {"contractVersion":1,"siteId":1,"mode":"Online","revision":8,"effectiveAtUtc":"2026-09-07T10:00:00+00:00","expiresAtUtc":"2026-10-07T21:00:00+00:00","siteName":"YAGOT","siteUrl":"https://example.test","startDate":"2026-09-07","originalDurationDays":30}
            """;
        var body = caseName switch
        {
            "malformed" => "{",
            "wrong-version" => valid.Replace(
                "\"contractVersion\":1",
                "\"contractVersion\":2",
                StringComparison.Ordinal),
            "wrong-site" => valid.Replace(
                "\"siteId\":1",
                "\"siteId\":2",
                StringComparison.Ordinal),
            "wrong-mode" => valid.Replace(
                "\"mode\":\"Online\"",
                "\"mode\":\"online\"",
                StringComparison.Ordinal),
            "missing-property" => valid.Replace(
                ",\"siteName\":\"YAGOT\"",
                string.Empty,
                StringComparison.Ordinal),
            "unknown-property" => valid.Replace(
                "{",
                "{\"extra\":true,",
                StringComparison.Ordinal),
            "duplicate-property" => valid.Replace(
                "{",
                "{\"siteId\":1,",
                StringComparison.Ordinal),
            _ => throw new ArgumentOutOfRangeException(nameof(caseName))
        };
        return System.Text.Encoding.UTF8.GetBytes(body);
    }

    private sealed class QueueSnapshotClient(
        params ControlPanelSnapshotClientResult[] results) :
        IControlPanelSnapshotClient
    {
        private readonly Queue<ControlPanelSnapshotClientResult> _results =
            new(results);

        public int CallCount { get; private set; }

        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class CancellingSnapshotClient : IControlPanelSnapshotClient
    {
        public Task<ControlPanelSnapshotClientResult> GetSnapshotAsync(
            CancellationToken cancellationToken) =>
            Task.FromCanceled<ControlPanelSnapshotClientResult>(
                cancellationToken);
    }

    private sealed class CapturingApplyService(
        SiteStateApplyOutcome outcome) : ISiteStateApplyService
    {
        public int CallCount { get; private set; }
        public SiteStateSnapshotV1? Snapshot { get; private set; }
        public SiteStateDeliveryContext? Delivery { get; private set; }

        public Task<SiteStateApplyResult> ApplyAsync(
            SiteStateSnapshotV1 snapshot,
            SiteStateDeliveryContext? delivery = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Snapshot = snapshot;
            Delivery = delivery;
            return Task.FromResult(new SiteStateApplyResult(outcome));
        }
    }

    private sealed class ThrowingApplyService : ISiteStateApplyService
    {
        public Task<SiteStateApplyResult> ApplyAsync(
            SiteStateSnapshotV1 snapshot,
            SiteStateDeliveryContext? delivery = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("sensitive-database-detail");
    }

    private class CapturingDiagnosticsStore : ISiteStateSyncDiagnosticsStore
    {
        public int SuccessCount { get; private set; }
        public int FailureCount { get; private set; }
        public long? LastObservedRevision { get; private set; }

        public virtual Task RecordAttemptAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public virtual Task RecordSuccessAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            DateTimeOffset completedAtUtc,
            long remoteRevision,
            CancellationToken cancellationToken)
        {
            SuccessCount++;
            LastObservedRevision = remoteRevision;
            return Task.CompletedTask;
        }

        public virtual Task RecordFailureAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            DateTimeOffset completedAtUtc,
            long? remoteRevision,
            string failureCode,
            CancellationToken cancellationToken)
        {
            FailureCount++;
            LastObservedRevision = remoteRevision;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDiagnosticsStore : CapturingDiagnosticsStore
    {
        public override Task RecordAttemptAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive-checkpoint-detail");

        public override Task RecordSuccessAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            DateTimeOffset completedAtUtc,
            long remoteRevision,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("sensitive-checkpoint-detail");
    }

    private sealed class ImmediatePolicy : ISiteStateReconciliationPolicy
    {
        public List<int> RetryAttempts { get; } = [];
        public TimeSpan GetStartupDelay() => TimeSpan.Zero;
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.Zero;
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) =>
            TimeSpan.Zero;

        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter)
        {
            RetryAttempts.Add(nextAttempt);
            return TimeSpan.Zero;
        }
    }

    private sealed class CapturingLogger :
        ILogger<SiteStateReconciliationService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
