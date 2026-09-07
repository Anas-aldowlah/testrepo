using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class LocalSiteRuntimeStateProviderTests
{
    [Fact]
    public async Task DurableMissing_RemainsMissing_WithoutErasingRevisionOrPayloadGuards()
    {
        await using var rig = new RuntimeTestRig();
        rig.Read = _ => Task.FromResult(RuntimeTestRig.Found(5));
        await rig.Runtime.ReadAsync();
        rig.Clock.Advance(TimeSpan.FromSeconds(30));
        rig.Read = _ => Task.FromResult(LocalSiteStateReadResult.Missing);
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await rig.Runtime.ReadAsync()).Status);
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await rig.Runtime.ReadAsync()).Status);
        Assert.Equal(2, rig.Reads);
        rig.Clock.Advance(TimeSpan.FromSeconds(30));
        rig.Read = _ => Task.FromResult(RuntimeTestRig.Found(4));
        var regression = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(LocalSiteRuntimeStateReadFailure.RevisionRegression, regression.Category);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        rig.Read = _ => Task.FromResult(LocalSiteStateReadResult.Found(SiteStateContractTests.ValidSnapshot(revision: 5, siteName: "Conflict")));
        var conflict = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(LocalSiteRuntimeStateReadFailure.EqualRevisionConflict, conflict.Category);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LazyRead_ExactAbsoluteTtl_NoSlidingExtension(bool missing)
    {
        await using var rig = new RuntimeTestRig();
        var expected = missing ? LocalSiteStateReadResult.Missing : RuntimeTestRig.Found(1);
        rig.Read = _ => Task.FromResult(expected);
        Assert.Equal(0, rig.Reads);
        Assert.Equal(expected, await rig.Runtime.ReadAsync());
        rig.Clock.Advance(TimeSpan.FromSeconds(20));
        Assert.Same(expected, await rig.Runtime.ReadAsync());
        rig.Clock.Advance(TimeSpan.FromSeconds(9.999));
        Assert.Same(expected, await rig.Runtime.ReadAsync());
        Assert.Equal(1, rig.Reads);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(expected, await rig.Runtime.ReadAsync());
        Assert.Equal(2, rig.Reads);
        Assert.Equal(2, rig.DisposedReaders);
    }

    [Fact]
    public async Task ExactContractFields_PreserveExpiredMode_AndNoHttpServices()
    {
        await using var rig = new RuntimeTestRig();
        var snapshot = SiteStateContractTests.ValidSnapshot(revision: 19) with
        {
            ExpiresAtUtc = DateTimeOffset.Parse("2001-01-01T21:00:00Z"),
            SiteName = "اسم الموقع", SiteUrl = "https://site.example/a?b=1"
        };
        rig.Read = _ => Task.FromResult(LocalSiteStateReadResult.Found(snapshot));
        Assert.Null(rig.Services.GetService<HttpClient>());
        Assert.Null(rig.Services.GetService<IControlPanelSnapshotClient>());
        Assert.Equal(snapshot, (await rig.Runtime.ReadAsync()).Snapshot);
        Assert.Equal(snapshot.Mode, (await rig.Runtime.ReadAsync()).Snapshot!.Mode);
        Assert.Equal(1, rig.Reads);
    }

    [Fact]
    public async Task DatabaseFailure_IsSanitized_AndSuppressedForExactlyOneSecond()
    {
        await using var rig = new RuntimeTestRig();
        rig.Read = _ => throw new InvalidOperationException("Password=SECRET; raw-body receipt-hash API-KEY");
        var first = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(LocalSiteRuntimeStateReadFailure.StorageUnavailable, first.Category);
        Assert.Null(first.InnerException);
        Assert.DoesNotContain("SECRET", first.ToString());
        for (var i = 0; i < 20; i++)
            await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        rig.Clock.Advance(TimeSpan.FromMilliseconds(999));
        await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(1, rig.Reads);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        rig.Read = _ => Task.FromResult(LocalSiteStateReadResult.Missing);
        Assert.Equal(LocalSiteStateReadStatus.Missing, (await rig.Runtime.ReadAsync()).Status);
        Assert.Equal(2, rig.Reads);
        Assert.NotEmpty(rig.Logs);
        Assert.All(rig.Logs, message =>
        {
            Assert.DoesNotContain("SECRET", message);
            Assert.DoesNotContain("raw-body", message);
            Assert.DoesNotContain("receipt-hash", message);
            Assert.DoesNotContain("API-KEY", message);
        });
    }

    [Fact]
    public async Task ExpiredCachedValue_IsNeverReturnedOnStorageFailure()
    {
        await using var rig = new RuntimeTestRig();
        await rig.Runtime.ReadAsync();
        rig.Clock.Advance(TimeSpan.FromSeconds(30));
        rig.Read = _ => throw new TimeoutException("sensitive database detail");
        var failure = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(LocalSiteRuntimeStateReadFailure.StorageUnavailable, failure.Category);
    }

    [Theory]
    [InlineData("wrong-site")]
    [InlineData("wrong-mode")]
    [InlineData("null-found")]
    [InlineData("invalid-status")]
    public async Task MalformedDurableResults_AreNotCached(string kind)
    {
        await using var rig = new RuntimeTestRig();
        var valid = SiteStateContractTests.ValidSnapshot();
        var invalid = kind switch
        {
            "wrong-site" => LocalSiteStateReadResult.Found(valid with { SiteId = 2 }),
            "wrong-mode" => LocalSiteStateReadResult.Found(valid with { Mode = "made-up" }),
            "null-found" => new LocalSiteStateReadResult(LocalSiteStateReadStatus.Found, null),
            _ => new LocalSiteStateReadResult((LocalSiteStateReadStatus)99, valid)
        };
        rig.Read = _ => Task.FromResult(invalid);
        var failure = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(LocalSiteRuntimeStateReadFailure.InvalidDurableState, failure.Category);
    }

    [Theory]
    [InlineData(4, false, LocalSiteRuntimeStateReadFailure.RevisionRegression)]
    [InlineData(5, true, LocalSiteRuntimeStateReadFailure.EqualRevisionConflict)]
    public async Task RefreshCannotRegressOrChangeEqualPayload(long revision, bool change, LocalSiteRuntimeStateReadFailure category)
    {
        await using var rig = new RuntimeTestRig();
        rig.Read = _ => Task.FromResult(RuntimeTestRig.Found(5));
        await rig.Runtime.ReadAsync();
        rig.Clock.Advance(TimeSpan.FromSeconds(30));
        rig.Read = _ => Task.FromResult(LocalSiteStateReadResult.Found(
            SiteStateContractTests.ValidSnapshot(revision: revision) with { SiteName = change ? "Changed" : "YAGOT" }));
        var failure = await Assert.ThrowsAsync<LocalSiteRuntimeStateReadException>(() => rig.Runtime.ReadAsync());
        Assert.Equal(category, failure.Category);
    }

    [Theory]
    [InlineData(SiteStateApplyOutcome.Stale)]
    [InlineData(SiteStateApplyOutcome.EqualConflict)]
    [InlineData(SiteStateApplyOutcome.DeliveryIdPayloadConflict)]
    [InlineData(SiteStateApplyOutcome.DuplicateDelivery)]
    [InlineData(SiteStateApplyOutcome.Equal)]
    public async Task NonNewerOutcome_DoesNotReplaceOrExtendTtl(SiteStateApplyOutcome outcome)
    {
        await using var rig = new RuntimeTestRig();
        await rig.Runtime.ReadAsync();
        rig.Clock.Advance(TimeSpan.FromSeconds(29));
        await rig.Decorate(new OutcomeWriter(new(outcome))).ApplyAsync(SiteStateContractTests.ValidSnapshot());
        await rig.Runtime.ReadAsync();
        Assert.Equal(1, rig.Reads);
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        await rig.Runtime.ReadAsync();
        Assert.Equal(2, rig.Reads);
    }

    [Theory]
    [InlineData(SiteStateApplyOutcome.Applied)]
    [InlineData(SiteStateApplyOutcome.Equal)]
    public async Task CommittedRevision_InvalidatesOlderEntry(SiteStateApplyOutcome outcome)
    {
        await using var rig = new RuntimeTestRig();
        await rig.Runtime.ReadAsync();
        rig.Read = _ => Task.FromResult(RuntimeTestRig.Found(2));
        await rig.Decorate(new OutcomeWriter(new(outcome))).ApplyAsync(SiteStateContractTests.ValidSnapshot(revision: 2));
        Assert.Equal(2, (await rig.Runtime.ReadAsync()).Snapshot!.Revision);
        Assert.Equal(2, rig.Reads);
    }

    [Fact]
    public async Task DuplicateApplied_InvalidatesWithoutTrustingIncomingPayload()
    {
        await using var rig = new RuntimeTestRig();
        await rig.Runtime.ReadAsync();
        rig.Read = _ => Task.FromResult(RuntimeTestRig.Found(2));
        await rig.Decorate(new OutcomeWriter(new(SiteStateApplyOutcome.DuplicateDelivery, SiteStateReceiptDecision.Applied)))
            .ApplyAsync(SiteStateContractTests.ValidSnapshot(revision: 999, siteName: "Never publish"));
        Assert.Equal(2, (await rig.Runtime.ReadAsync()).Snapshot!.Revision);
    }

    [Fact]
    public async Task FailedWriter_DoesNotInvalidate_AndFailedNotificationCannotFailCommit()
    {
        await using var rig = new RuntimeTestRig();
        await rig.Runtime.ReadAsync();
        await Assert.ThrowsAsync<TimeoutException>(() => rig.Decorate(new ThrowingWriter()).ApplyAsync(SiteStateContractTests.ValidSnapshot()));
        await rig.Runtime.ReadAsync();
        Assert.Equal(1, rig.Reads);
        var decorator = new SiteStateRuntimeAwareApplyService(new OutcomeWriter(new(SiteStateApplyOutcome.Applied)),
            new ThrowingInvalidator(), NullLogger<SiteStateRuntimeAwareApplyService>.Instance);
        Assert.Equal(SiteStateApplyOutcome.Applied, (await decorator.ApplyAsync(SiteStateContractTests.ValidSnapshot())).Outcome);
    }

    private sealed class ThrowingWriter : ISiteStateApplyService
    {
        public Task<SiteStateApplyResult> ApplyAsync(SiteStateSnapshotV1 snapshot, SiteStateDeliveryContext? delivery = null,
            CancellationToken cancellationToken = default) => throw new TimeoutException();
    }
    private sealed class ThrowingInvalidator : ILocalSiteRuntimeStateInvalidator
    {
        public void ObserveCommittedRevision(long revision) => throw new Exception("SECRET");
        public void InvalidateForCommittedDelivery() => throw new Exception("SECRET");
    }
}

internal sealed class OutcomeWriter(SiteStateApplyResult result) : ISiteStateApplyService
{
    public Task<SiteStateApplyResult> ApplyAsync(SiteStateSnapshotV1 snapshot, SiteStateDeliveryContext? delivery = null,
        CancellationToken cancellationToken = default) => Task.FromResult(result);
}

internal sealed class RuntimeTestRig : IAsyncDisposable
{
    public FakeTimeProvider Clock { get; } = new();
    public ServiceProvider Services { get; }
    public ILocalSiteRuntimeStateProvider Runtime => Services.GetRequiredService<ILocalSiteRuntimeStateProvider>();
    public ILocalSiteRuntimeStateInvalidator Invalidator => Services.GetRequiredService<ILocalSiteRuntimeStateInvalidator>();
    public Func<CancellationToken, Task<LocalSiteStateReadResult>> Read { get; set; } = _ => Task.FromResult(Found(1));
    public int Reads;
    public int DisposedReaders;
    public List<string> Logs { get; } = [];

    public RuntimeTestRig()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(Clock);
        services.AddScoped<ILocalSiteStateReader>(_ => new Reader(this));
        services.AddLogging(builder => builder.AddProvider(new LogSink(Logs)));
        services.AddLocalSiteRuntimeState();
        Services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public ISiteStateApplyService Decorate(ISiteStateApplyService writer) => new SiteStateRuntimeAwareApplyService(
        writer, Invalidator, NullLogger<SiteStateRuntimeAwareApplyService>.Instance);
    public static LocalSiteStateReadResult Found(long revision) => LocalSiteStateReadResult.Found(SiteStateContractTests.ValidSnapshot(revision: revision));
    public ValueTask DisposeAsync() => Services.DisposeAsync();

    private sealed class Reader(RuntimeTestRig owner) : ILocalSiteStateReader, IDisposable
    {
        public Task<LocalSiteStateReadResult> ReadAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref owner.Reads);
            return owner.Read(cancellationToken);
        }
        public void Dispose() => Interlocked.Increment(ref owner.DisposedReaders);
    }
    private sealed class LogSink(List<string> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new EntryLogger(entries);
        public void Dispose() { }
        private sealed class EntryLogger(List<string> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (entries) entries.Add(formatter(state, exception));
            }
        }
    }
}
