using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationCoordinatorTests
{
    [Fact]
    public async Task ConcurrentTrigger_IsCoalescedWithoutSecondExecution()
    {
        var controlled = new ControlledReconciliationService();
        await using var provider = new ServiceCollection()
            .AddScoped<ISiteStateReconciliationService>(_ => controlled)
            .BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateScopes = true
            });
        using var coordinator = new SiteStateReconciliationCoordinator(
            provider.GetRequiredService<IServiceScopeFactory>());

        var first = coordinator.RequestAsync(
            SiteStateReconciliationReason.Startup,
            CancellationToken.None);
        await controlled.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var second = await coordinator.RequestAsync(
            SiteStateReconciliationReason.Periodic,
            CancellationToken.None);

        Assert.Equal(
            SiteStateReconciliationTriggerStatus.AlreadyRunning,
            second.Status);
        Assert.Null(second.Result);
        Assert.Equal(1, controlled.CallCount);

        controlled.Release.TrySetResult();
        var completed = await first;
        Assert.Equal(
            SiteStateReconciliationTriggerStatus.Completed,
            completed.Status);
    }

    [Fact]
    public async Task BackgroundService_WaitsForApplicationStartedWithoutBlockingStart()
    {
        var lifetime = new TestApplicationLifetime();
        var coordinator = new CapturingCoordinator();
        var background = new SiteStateReconciliationBackgroundService(
            lifetime,
            coordinator,
            new BackgroundTestPolicy(),
            TimeProvider.System,
            NullLogger<SiteStateReconciliationBackgroundService>.Instance);

        await background.StartAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, coordinator.CallCount);

        lifetime.NotifyStarted();
        await coordinator.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(1, coordinator.CallCount);
        Assert.Equal(SiteStateReconciliationReason.Startup, coordinator.LastReason);
        await background.StopAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(2));
    }

    private sealed class ControlledReconciliationService :
        ISiteStateReconciliationService
    {
        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }

        public async Task<SiteStateReconciliationResult> ReconcileAsync(
            SiteStateReconciliationReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return SiteStateReconciliationResult.Success(
                SiteStateApplyOutcome.Equal,
                1,
                1);
        }
    }

    private sealed class CapturingCoordinator :
        ISiteStateReconciliationCoordinator
    {
        public TaskCompletionSource Called { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }
        public SiteStateReconciliationReason? LastReason { get; private set; }

        public Task<SiteStateReconciliationTriggerResult> RequestAsync(
            SiteStateReconciliationReason reason,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastReason = reason;
            Called.TrySetResult();
            return Task.FromResult(new SiteStateReconciliationTriggerResult(
                SiteStateReconciliationTriggerStatus.Completed,
                SiteStateReconciliationResult.Success(
                    SiteStateApplyOutcome.Equal,
                    1,
                    1)));
        }
    }

    private sealed class BackgroundTestPolicy : ISiteStateReconciliationPolicy
    {
        public TimeSpan GetStartupDelay() => TimeSpan.Zero;
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.FromHours(1);
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) =>
            TimeSpan.FromHours(1);
        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter) => TimeSpan.Zero;
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
