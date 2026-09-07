using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Services.Integration;

public interface ISiteStateReconciliationCoordinator
{
    Task<SiteStateReconciliationTriggerResult> RequestAsync(
        SiteStateReconciliationReason reason,
        CancellationToken cancellationToken);
}

public sealed class SiteStateReconciliationCoordinator :
    ISiteStateReconciliationCoordinator,
    IDisposable
{
    private readonly SemaphoreSlim _executionGate = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;

    public SiteStateReconciliationCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<SiteStateReconciliationTriggerResult> RequestAsync(
        SiteStateReconciliationReason reason,
        CancellationToken cancellationToken)
    {
        if (!await _executionGate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return new SiteStateReconciliationTriggerResult(
                SiteStateReconciliationTriggerStatus.AlreadyRunning,
                null);
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider
                .GetRequiredService<ISiteStateReconciliationService>();
            var result = await service.ReconcileAsync(reason, cancellationToken);
            return new SiteStateReconciliationTriggerResult(
                SiteStateReconciliationTriggerStatus.Completed,
                result);
        }
        finally
        {
            _executionGate.Release();
        }
    }

    public void Dispose() => _executionGate.Dispose();
}
