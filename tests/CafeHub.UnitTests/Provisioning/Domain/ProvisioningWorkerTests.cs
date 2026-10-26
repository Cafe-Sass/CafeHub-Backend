using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Infrastructure.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Provisioning.Domain
{
    public sealed class ProvisioningWorkerTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => FixedNow;
        }

        private sealed class FakeClaimer : IProvisioningJobClaimer
        {
            public List<string> ClaimedBy { get; } = [];
            public List<DateTimeOffset> ClaimTimes { get; } = [];
            public List<TimeSpan> LeaseDurations { get; } = [];
            public Result<ClaimedProvisioningJob?> ClaimResult { get; set; } = Result.Success<ClaimedProvisioningJob?>(null);

            public Task<Result<ClaimedProvisioningJob?>> TryClaimNextAsync(
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

        private sealed class FakeRunner : IProvisioningJobRunner
        {
            public List<ClaimedProvisioningJob> RunJobs { get; } = [];
            public int FailuresBeforeSuccess { get; set; }

            public Task RunAsync(ClaimedProvisioningJob claimedJob, CancellationToken cancellationToken)
            {
                RunJobs.Add(claimedJob);
                if (RunJobs.Count <= FailuresBeforeSuccess)
                {
                    throw new InvalidOperationException("runner failure");
                }

                return Task.CompletedTask;
            }
        }

        private sealed class FakeLogger : ILogger<ProvisioningWorker>
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
                if (serviceType == typeof(IProvisioningJobClaimer))
                {
                    return _claimer;
                }

                if (serviceType == typeof(IProvisioningJobRunner))
                {
                    return _runner;
                }

                return null;
            }
        }

        private static ProvisioningWorkerOptions CreateOptions(bool enabled = true) =>
            new()
            {
                Enabled = enabled,
                PollIntervalSeconds = 1,
                LeaseSeconds = 30,
                WorkerId = "test-worker",
            };

        private static ProvisioningWorker CreateWorker(
            FakeClaimer claimer,
            FakeRunner runner,
            ProvisioningWorkerOptions options,
            FakeLogger logger)
        {
            return new ProvisioningWorker(
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
            Assert.Empty(runner.RunJobs);
        }

        [Fact]
        public async Task Worker_ClaimsAndRunsDueJobOnce()
        {
            var claimedJob = new ClaimedProvisioningJob(Guid.CreateVersion7(), Guid.CreateVersion7());
            var claimer = new FakeClaimer { ClaimResult = Result.Success<ClaimedProvisioningJob?>(claimedJob) };
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Single(claimer.ClaimedBy);
            Assert.Equal("test-worker", claimer.ClaimedBy[0]);
            Assert.Equal(FixedNow, claimer.ClaimTimes[0]);
            Assert.Equal(TimeSpan.FromSeconds(30), claimer.LeaseDurations[0]);
            Assert.Single(runner.RunJobs);
            Assert.Equal(claimedJob, runner.RunJobs[0]);
        }

        [Fact]
        public async Task Worker_ClaimsNothingWhenNoJobIsDue()
        {
            var claimer = new FakeClaimer();
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Single(claimer.ClaimedBy);
            Assert.Empty(runner.RunJobs);
        }

        [Fact]
        public async Task Worker_ContinuesAfterRunnerFailure()
        {
            var claimedJob = new ClaimedProvisioningJob(Guid.CreateVersion7(), Guid.CreateVersion7());
            var claimer = new FakeClaimer { ClaimResult = Result.Success<ClaimedProvisioningJob?>(claimedJob) };
            var runner = new FakeRunner { FailuresBeforeSuccess = 1 };
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);
            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Equal(2, claimer.ClaimedBy.Count);
            Assert.Equal(2, runner.RunJobs.Count);
            Assert.Contains(logger.Messages, m => m.Contains(claimedJob.JobId.ToString(), StringComparison.Ordinal));
        }

        [Fact]
        public async Task Worker_ContinuesAfterClaimFailure()
        {
            var claimer = new FakeClaimer
            {
                ClaimResult = Result.Failure<ClaimedProvisioningJob?>(ProvisioningErrors.InvalidWorkerId()),
            };
            var runner = new FakeRunner();
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);
            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.Equal(2, claimer.ClaimedBy.Count);
            Assert.Empty(runner.RunJobs);
            Assert.Contains(logger.Messages, m => m.Contains(ProvisioningErrors.InvalidWorkerIdCode, StringComparison.Ordinal));
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
            Assert.Empty(runner.RunJobs);
        }

        [Fact]
        public async Task Worker_LogsContainNoSecrets()
        {
            var claimedJob = new ClaimedProvisioningJob(Guid.CreateVersion7(), Guid.CreateVersion7());
            var claimer = new FakeClaimer { ClaimResult = Result.Success<ClaimedProvisioningJob?>(claimedJob) };
            var runner = new FakeRunner { FailuresBeforeSuccess = 1 };
            var logger = new FakeLogger();
            var worker = CreateWorker(claimer, runner, CreateOptions(), logger);

            await worker.ExecuteOnceAsync(CancellationToken.None);

            Assert.DoesNotContain(logger.Messages, m => m.Contains("password", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Messages, m => m.Contains("connection", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(logger.Messages, m => m.Contains("secret", StringComparison.OrdinalIgnoreCase));
        }
    }
}
