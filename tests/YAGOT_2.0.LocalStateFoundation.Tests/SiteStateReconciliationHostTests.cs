using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;
using Xunit;

namespace YAGOT_2._0.LocalStateFoundation.Tests;

public sealed class SiteStateReconciliationHostTests
{
    private const string TestApiKey = "host-only-snapshot-key-marker";

    [Fact]
    public async Task ProductionRegistration_MissingBaseUrlFailsHostStartup()
    {
        var configuration = ValidConfiguration();
        configuration.Remove("YagotIntegration:ControlPanelBaseUrl");

        await AssertStartupValidationFailureAsync(
            configuration,
            "YagotIntegration:ControlPanelBaseUrl");
    }

    [Fact]
    public async Task ProductionRegistration_MissingApiKeyFailsHostStartup()
    {
        var configuration = ValidConfiguration();
        configuration.Remove("YagotIntegration:SnapshotApiKey");

        await AssertStartupValidationFailureAsync(
            configuration,
            "YagotIntegration:SnapshotApiKey");
    }

    [Fact]
    public async Task ProductionRegistration_InvalidConfigurationFailsHostStartup()
    {
        var configuration = ValidConfiguration();
        configuration["YagotIntegration:ControlPanelBaseUrl"] =
            "http://control-panel.test/path";

        await AssertStartupValidationFailureAsync(
            configuration,
            "YagotIntegration:ControlPanelBaseUrl");
    }

    [Fact]
    public async Task ProductionRegistration_ValidExternalConfigurationStartsHost()
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(ValidConfiguration(), logs);

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(host.Services.GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStarted.IsCancellationRequested);
        Assert.NotNull(host.Services.GetRequiredService<IHttpClientFactory>());
        Assert.IsType<ControlPanelSnapshotClient>(
            host.Services.GetRequiredService<IControlPanelSnapshotClient>());
        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(TestApiKey, logs.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnavailableControlPanel_RunsAfterApplicationStartedAndDoesNotStopHost()
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(
            ValidConfiguration(),
            logs,
            services =>
            {
                services.AddSingleton<FailingControlPanelHandler>();
                services.AddHttpClient<
                        IControlPanelSnapshotClient,
                        ControlPanelSnapshotClient>()
                    .ConfigurePrimaryHttpMessageHandler(provider =>
                        provider.GetRequiredService<FailingControlPanelHandler>());
                services.Replace(ServiceDescriptor.Scoped<
                    ISiteStateSyncDiagnosticsStore,
                    NoOpDiagnosticsStore>());
                services.Replace(ServiceDescriptor.Singleton<
                    ISiteStateReconciliationPolicy,
                    ImmediateStartupPolicy>());
            });
        var handler = host.Services.GetRequiredService<FailingControlPanelHandler>();

        await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await handler.FailedCycleCompleted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        Assert.True(handler.ApplicationWasStartedOnEveryCall);
        Assert.Equal(3, handler.CallCount);
        Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
        Assert.False(lifetime.ApplicationStopped.IsCancellationRequested);

        await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(TestApiKey, logs.Output, StringComparison.Ordinal);
    }

    private static async Task AssertStartupValidationFailureAsync(
        Dictionary<string, string?> configuration,
        string expectedFailure)
    {
        var logs = new CapturingLoggerProvider();
        using var host = BuildHost(configuration, logs);

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync());

        Assert.Contains(expectedFailure, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(TestApiKey, exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(TestApiKey, logs.Output, StringComparison.Ordinal);
    }

    private static IHost BuildHost(
        Dictionary<string, string?> configuration,
        CapturingLoggerProvider logs,
        Action<IServiceCollection>? customize = null) =>
        Host.CreateDefaultBuilder([])
            .ConfigureAppConfiguration(builder =>
            {
                builder.Sources.Clear();
                builder.AddInMemoryCollection(configuration);
            })
            .ConfigureLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddProvider(logs);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton<ISiteStateSnapshotV1JsonParser,
                    SiteStateSnapshotV1JsonParser>();
                services.AddSingleton<ISiteStateApplyService, NoOpApplyService>();
                services.AddSiteStateReconciliation(context.Configuration);
                services.Replace(ServiceDescriptor.Scoped<
                    ISiteStateSyncDiagnosticsStore,
                    NoOpDiagnosticsStore>());
                services.Replace(ServiceDescriptor.Singleton<
                    ISiteStateReconciliationPolicy,
                    DelayedStartupPolicy>());
                customize?.Invoke(services);
            })
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateScopes = true;
                options.ValidateOnBuild = true;
            })
            .Build();

    private static Dictionary<string, string?> ValidConfiguration() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["YagotIntegration:SiteId"] = "1",
            ["YagotIntegration:ControlPanelBaseUrl"] =
                "https://control-panel.test/",
            ["YagotIntegration:SnapshotApiKey"] = TestApiKey,
            ["YagotIntegration:ReconciliationIntervalMinutes"] = "30",
            ["YagotIntegration:SnapshotHttpTimeoutSeconds"] = "10"
        };

    private sealed class NoOpApplyService : ISiteStateApplyService
    {
        public Task<SiteStateApplyResult> ApplyAsync(
            SiteStateSnapshotV1 snapshot,
            SiteStateDeliveryContext? delivery = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SiteStateApplyResult(SiteStateApplyOutcome.Equal));
    }

    private sealed class NoOpDiagnosticsStore : ISiteStateSyncDiagnosticsStore
    {
        public Task RecordAttemptAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordSuccessAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            DateTimeOffset completedAtUtc,
            long remoteRevision,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task RecordFailureAsync(
            int siteId,
            DateTimeOffset attemptedAtUtc,
            DateTimeOffset completedAtUtc,
            long? remoteRevision,
            string failureCode,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DelayedStartupPolicy : ISiteStateReconciliationPolicy
    {
        public TimeSpan GetStartupDelay() => TimeSpan.FromDays(1);
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.FromDays(1);
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) =>
            TimeSpan.FromDays(1);
        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter) => TimeSpan.Zero;
    }

    private sealed class ImmediateStartupPolicy : ISiteStateReconciliationPolicy
    {
        public TimeSpan GetStartupDelay() => TimeSpan.Zero;
        public TimeSpan GetSuccessfulCycleDelay() => TimeSpan.FromDays(1);
        public TimeSpan GetFailedCycleDelay(int consecutiveFailures) =>
            TimeSpan.FromDays(1);
        public TimeSpan? GetInlineRetryDelay(
            int nextAttempt,
            TimeSpan? retryAfter) => TimeSpan.Zero;
    }

    private sealed class FailingControlPanelHandler : HttpMessageHandler
    {
        private readonly IHostApplicationLifetime _lifetime;
        private int _callCount;
        private int _applicationWasStartedOnEveryCall = 1;

        public FailingControlPanelHandler(IHostApplicationLifetime lifetime) =>
            _lifetime = lifetime;

        public TaskCompletionSource FailedCycleCompleted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount => Volatile.Read(ref _callCount);
        public bool ApplicationWasStartedOnEveryCall =>
            Volatile.Read(ref _applicationWasStartedOnEveryCall) == 1;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (!_lifetime.ApplicationStarted.IsCancellationRequested)
            {
                Interlocked.Exchange(ref _applicationWasStartedOnEveryCall, 0);
            }

            if (Interlocked.Increment(ref _callCount) == 3)
            {
                FailedCycleCompleted.TrySetResult();
            }

            return Task.FromResult(new HttpResponseMessage(
                HttpStatusCode.InternalServerError));
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];
        public string Output
        {
            get
            {
                lock (_messages)
                {
                    return string.Join(Environment.NewLine, _messages);
                }
            }
        }

        public ILogger CreateLogger(string categoryName) =>
            new CapturingLogger(_messages);
        public void Dispose() { }

        private sealed class CapturingLogger(List<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                lock (messages)
                {
                    messages.Add(formatter(state, exception));
                }
            }
        }
    }
}
