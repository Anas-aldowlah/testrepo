using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationBackgroundServiceTests
{
    [Fact]
    public async Task HostedScheduler_FailuresBackOffAndSuccessResetsBaseDelay()
    {
        var lifetime = new TestApplicationLifetime();
        var coordinator = new SequencedCoordinator(false, false, false, true);
        var policy = new RecordingSchedulePolicy();
        var time = new FakeTimeProvider(
            new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
        var background = new SiteStateReconciliationBackgroundService(
            lifetime,
            coordinator,
            policy,
            time,
            NullLogger<SiteStateReconciliationBackgroundService>.Instance);
        lifetime.NotifyStarted();

        await background.StartAsync(CancellationToken.None);
        await policy.WaitForDelayCountAsync(1);

        Assert.Equal([1], policy.FailedCycleArguments);
        Assert.Equal([TimeSpan.FromMinutes(33)], policy.ScheduledDelays);
        Assert.Equal(1, coordinator.CallCount);

        time.Advance(TimeSpan.FromMinutes(32) + TimeSpan.FromSeconds(59));
        await Task.Yield();
        Assert.Equal(1, coordinator.CallCount);
        time.Advance(TimeSpan.FromSeconds(1));
        await policy.WaitForDelayCountAsync(2);

        Assert.Equal([1, 2], policy.FailedCycleArguments);
        Assert.Equal(TimeSpan.FromMinutes(64), policy.ScheduledDelays[1]);
        Assert.Equal(2, coordinator.CallCount);

        time.Advance(TimeSpan.FromMinutes(63) + TimeSpan.FromSeconds(59));
        await Task.Yield();
        Assert.Equal(2, coordinator.CallCount);
        time.Advance(TimeSpan.FromSeconds(1));
        await policy.WaitForDelayCountAsync(3);

        Assert.Equal([1, 2, 3], policy.FailedCycleArguments);
        Assert.Equal(TimeSpan.FromMinutes(122), policy.ScheduledDelays[2]);
        Assert.Equal(3, coordinator.CallCount);

        time.Advance(TimeSpan.FromMinutes(121) + TimeSpan.FromSeconds(59));
        await Task.Yield();
        Assert.Equal(3, coordinator.CallCount);
        time.Advance(TimeSpan.FromSeconds(1));
        await policy.WaitForDelayCountAsync(4);

        Assert.Equal(4, coordinator.CallCount);
        Assert.Equal(TimeSpan.FromMinutes(31), policy.ScheduledDelays[3]);
        Assert.InRange(
            policy.ScheduledDelays[0],
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(35));
        Assert.InRange(
            policy.ScheduledDelays[1],
            TimeSpan.FromMinutes(60),
            TimeSpan.FromMinutes(65));
        Assert.InRange(
            policy.ScheduledDelays[2],
            TimeSpan.FromMinutes(120),
            TimeSpan.FromMinutes(125));
        Assert.InRange(
            policy.ScheduledDelays[3],
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(35));

        await background.StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class SequencedCoordinator(params bool[] outcomes) :
        ISiteStateReconciliationCoordinator
    {
        private readonly Queue<bool> _outcomes = new(outcomes);
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<SiteStateReconciliationTriggerResult> RequestAsync(
            SiteStateReconciliationReason reason,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            var succeeded = _outcomes.Dequeue();
            var result = succeeded
                ? SiteStateReconciliationResult.Success(
                    SiteStateApplyOutcome.Equal,
                    10,
                    1)
                : SiteStateReconciliationResult.Failure(
                    SiteStateReconciliationFailureCodes.Network,
                    3);
            return Task.FromResult(new SiteStateReconciliationTriggerResult(
                SiteStateReconciliationTriggerStatus.Completed,
                result));
        }
    }

    private sealed class RecordingSchedulePolicy :
        ISiteStateReconciliationPolicy
    {
        private readonly SemaphoreSlim _delayRecorded = new(0);

        public List<int> FailedCycleArguments { get; } = [];
        public List<TimeSpan> ScheduledDelays { get; } = [];

        public TimeSpan GetStartupDelay() => TimeSpan.Zero;

        public TimeSpan GetSuccessfulCycleDelay()
        {
            var delay = TimeSpan.FromMinutes(31);
            Record(delay);
            return delay;
        }

        public TimeSpan GetFailedCycleDelay(int consecutiveFailures)
        {
            FailedCycleArguments.Add(consecutiveFailures);
            var delay = consecutiveFailures switch
            {
                1 => TimeSpan.FromMinutes(33),
                2 => TimeSpan.FromMinutes(64),
                3 => TimeSpan.FromMinutes(122),
                _ => throw new Xunit.Sdk.XunitException(
                    $"Unexpected failure count {consecutiveFailures}.")
            };
            Record(delay);
            return delay;
        }

        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter) => TimeSpan.Zero;

        public async Task WaitForDelayCountAsync(int expected)
        {
            while (ScheduledDelays.Count < expected)
            {
                await _delayRecorded.WaitAsync().WaitAsync(TimeSpan.FromSeconds(2));
            }
        }

        private void Record(TimeSpan delay)
        {
            ScheduledDelays.Add(delay);
            _delayRecorded.Release();
        }
    }

    private sealed class TestApplicationLifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource _started = new();
        private readonly CancellationTokenSource _stopping = new();
        private readonly CancellationTokenSource _stopped = new();

        public CancellationToken ApplicationStarted => _started.Token;
        public CancellationToken ApplicationStopping => _stopping.Token;
        public CancellationToken ApplicationStopped => _stopped.Token;
        public void StopApplication() => _stopping.Cancel();
        public void NotifyStarted() => _started.Cancel();
    }
}
