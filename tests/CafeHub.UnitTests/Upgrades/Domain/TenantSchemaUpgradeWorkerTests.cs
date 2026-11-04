using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Contracts;
using CafeHub.Infrastructure.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Upgrades.Domain
{
    public sealed class TenantSchemaUpgradeWorkerTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => FixedNow;
        }

        private sealed class FakeClaimer : ITenantSchemaUpgradeRunClaimer
        {
            public List<string> ClaimedBy { get; } = [];
            public List<DateTimeOffset> ClaimTimes { get; } = [];
            public List<TimeSpan> LeaseDurations { get; } = [];
            public Result<ClaimedTenantSchemaUpgradeRun?> ClaimResult { get; set; } =
                Result.Success<ClaimedTenantSchemaUpgradeRun?>(null);

            public Task<Result<ClaimedTenantSchemaUpgradeRun?>> TryClaimNextAsync(
                string workerId,
                DateTimeOffset now,
                TimeSpan leaseDuration,
                CancellationToken cancellationToken)
            {
                ClaimedBy.Add(workerId);
                ClaimTimes.Add(now);
                LeaseDurations.Add(leaseDuration);
                return Task.FromResult(ClaimResult);
            }
        }

        private sealed class FakeRunner : ITenantSchemaUpgradeRunner
        {
            public List<ClaimedTenantSchemaUpgradeRun> Runs { get; } = [];
            public int FailuresBeforeSuccess { get; set; }

            public Task RunAsync(ClaimedTenantSchemaUpgradeRun claimedRun, CancellationToken cancellationToken)
            {
                Runs.Add(claimedRun);
                if (Runs.Count <= FailuresBeforeSuccess)
                {
                    throw new InvalidOperationException("runner failure");
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeLogger : ILogger<TenantSchemaUpgradeWorker>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception)!);
            }
        }

        private sealed class ScopeFactoryStub : IServiceScopeFactory
        {
            private readonly FakeClaimer _claimer;
            private readonly FakeRunner _runner;

            public ScopeFactoryStub(FakeClaimer claimer, FakeRunner runner)
            {
                _claimer = claimer;
                _runner = runner;
            }

            public IServiceScope CreateScope() => new ServiceScopeStub(_claimer, _runner);
        }

        private sealed class ServiceScopeStub : IServiceScope
        {
            public ServiceScopeStub(FakeClaimer claimer, FakeRunner runner)
            {
                ServiceProvider = new ServiceProviderStub(claimer, runner);
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose()
            {
            }
        }

        private sealed class ServiceProviderStub : IServiceProvider
        {
            private readonly FakeClaimer _claimer;
            private readonly FakeRunner _runner;

            public ServiceProviderStub(FakeClaimer claimer, FakeRunner runner)
            {
                _claimer = claimer;
                _runner = runner;
            }

            public object? GetService(Type serviceType)
            {
                if (serviceType == typeof(ITenantSchemaUpgradeRunClaimer))
                {
                    return _claimer;
                }

                if (serviceType == typeof(ITenantSchemaUpgradeRunner))
                {
                    return _runner;
                }

                return null;
            }
        }

        private static TenantSchemaUpgradeWorkerOptions CreateOptions(bool enabled = true) =>
            new()
            {
                Enabled = enabled,
                PollIntervalSeconds = 1,
                LeaseSeconds = 30,
                WorkerId = "test-worker",
            };

        private static TenantSchemaUpgradeWorker CreateWorker(
            FakeClaimer claimer,
            FakeRunner runner,
            TenantSchemaUpgradeWorkerOptions options,
            FakeLogger logger)
        {
            return new TenantSchemaUpgradeWorker(
                new ScopeFactoryStub(claimer, runner),
                options,
                logger,
                new FixedTimeProvider());
        }

        [Fact]
        public async Task Worker_DoesNotRunWhenDisabled()
        {
            var claimer = new FakeClaimer();
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(enabled: false), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Empty(claimer.ClaimedBy);
            Assert.Empty(runner.Runs);
        }

        [Fact]
        public async Task Worker_ClaimsAndRunsDueRunOnce()
        {
            var claimedRun = new ClaimedTenantSchemaUpgradeRun(Guid.CreateVersion7(), Guid.CreateVersion7());
            var claimer = new FakeClaimer
            {
                ClaimResult = Result.Success<ClaimedTenantSchemaUpgradeRun?>(claimedRun),
            };
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Single(claimer.ClaimedBy);
            Assert.Equal("test-worker", claimer.ClaimedBy[0]);
            Assert.Equal(FixedNow, claimer.ClaimTimes[0]);
            Assert.Equal(TimeSpan.FromSeconds(30), claimer.LeaseDurations[0]);
            Assert.Single(runner.Runs);
            Assert.Equal(claimedRun, runner.Runs[0]);
        }

        [Fact]
        public async Task Worker_ClaimsNothingWhenNoRunIsDue()
        {
            var claimer = new FakeClaimer();
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Single(claimer.ClaimedBy);
            Assert.Empty(runner.Runs);
        }

        [Fact]
        public async Task Worker_ContinuesAfterRunnerFailure()
        {
            var claimedRun = new ClaimedTenantSchemaUpgradeRun(Guid.CreateVersion7(), Guid.CreateVersion7());
            var claimer = new FakeClaimer
            {
                ClaimResult = Result.Success<ClaimedTenantSchemaUpgradeRun?>(claimedRun),
            };
            var runner = new FakeRunner { FailuresBeforeSuccess = 1 };
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);
            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Equal(2, claimer.ClaimedBy.Count);
            Assert.Equal(2, runner.Runs.Count);
            Assert.Contains(logger.Messages, message =>
                message.Contains(claimedRun.RunId.ToString(), StringComparison.Ordinal));
        }

        [Fact]
        public async Task Worker_ContinuesAfterClaimFailure()
        {
            var claimer = new FakeClaimer
            {
                ClaimResult = Result.Failure<ClaimedTenantSchemaUpgradeRun?>(
                    TenantSchemaUpgradeErrors.InvalidWorkerId()),
            };
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);
            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Equal(2, claimer.ClaimedBy.Count);
            Assert.Empty(runner.Runs);
            Assert.Contains(logger.Messages, message =>
                message.Contains(TenantSchemaUpgradeErrors.InvalidWorkerIdCode, StringComparison.Ordinal));
        }

        [Fact]
        public async Task Worker_StopsOnCancellation()
        {
            var claimer = new FakeClaimer();
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            await worker.ExecuteOnceAsync(cts.Token);

            Assert.Empty(claimer.ClaimedBy);
            Assert.Empty(runner.Runs);
        }

        [Fact]
        public async Task Worker_LogsContainNoSecrets()
        {
            var claimedRun = new ClaimedTenantSchemaUpgradeRun(Guid.CreateVersion7(), Guid.CreateVersion7());
            var claimer = new FakeClaimer
            {
                ClaimResult = Result.Success<ClaimedTenantSchemaUpgradeRun?>(claimedRun),
            };
            var runner = new FakeRunner { FailuresBeforeSuccess = 1 };
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.DoesNotContain(logger.Messages, message =>
                message.Contains("password", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Messages, message =>
                message.Contains("connection", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Messages, message =>
                message.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }
    }
}
