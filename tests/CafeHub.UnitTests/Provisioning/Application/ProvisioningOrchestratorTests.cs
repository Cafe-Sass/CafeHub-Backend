using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Domain.Onboarding;
using CafeHub.Domain.Provisioning;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Provisioning.Application
{
    public sealed class ProvisioningOrchestratorTests
    {
        private static readonly Guid JobId = Guid.CreateVersion7();
        private static readonly Guid TenantId = Guid.CreateVersion7();
        private static readonly Guid RequestId = Guid.CreateVersion7();
        private static readonly DateTimeOffset FixedNow = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

        private sealed class FakeJobStore : IProvisioningJobStore
        {
            public ProvisioningJob? Job { get; set; }
            public int UpdateCallCount { get; private set; }
            public List<ProvisioningJob> UpdatedJobs { get; } = [];

            public Task AddAsync(ProvisioningJob job, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<ProvisioningJob?> FindByIdAsync(Guid jobId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Job);
            }

            public Task<ProvisioningJob?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult<ProvisioningJob?>(null);
            }

            public Task UpdateAsync(ProvisioningJob job, CancellationToken cancellationToken)
            {
                UpdatedJobs.Add(job);
                UpdateCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeAttemptStore : IProvisioningJobAttemptStore
        {
            public List<ProvisioningJobAttempt> Attempts { get; } = [];

            public Task AddAsync(ProvisioningJobAttempt attempt, CancellationToken cancellationToken)
            {
                Attempts.Add(attempt);
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<ProvisioningJobAttempt>> FindByJobIdAsync(
                Guid jobId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<IReadOnlyList<ProvisioningJobAttempt>>(Attempts);
            }
        }

        private sealed class FakeTenantStore : ITenantStore
        {
            public Tenant? Tenant { get; set; }
            public int FindByIdCallCount { get; private set; }

            public Task<Tenant?> FindByOnboardingRequestIdAsync(
                Guid onboardingRequestId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<Tenant?>(null);
            }

            public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                FindByIdCallCount++;
                return Task.FromResult(Tenant);
            }

            public Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeTenantDatabaseStore : ITenantDatabaseStore
        {
            public TenantDatabase? Database { get; set; }

            public Task<TenantDatabase?> FindByTenantIdAsync(
                Guid tenantId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(Database);
            }

            public Task AddAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeOnboardingRequestStore : IOnboardingRequestStore
        {
            public OnboardingRequest? Request { get; set; }

            public Task<OnboardingRequest?> FindPublicByEmailAsync(
                string emailLower,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<OnboardingRequest?>(null);
            }

            public Task<OnboardingRequest?> FindByIdAsync(
                Guid id,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(Request);
            }

            public Task AddAsync(OnboardingRequest request, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(OnboardingRequest request, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<OnboardingRequestListPage> ListForReviewAsync(
                OnboardingRequestListRequest request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new OnboardingRequestListPage(new List<OnboardingRequest>(), 0));
            }
        }

        private sealed class FakeDatabaseCreator : ITenantDatabaseCreator
        {
            public int CreateCallCount { get; private set; }
            public Result<TenantDatabaseInfo> Result { get; set; } =
                global::CafeHub.Application.Abstractions.Results.Result.Success(
                    new TenantDatabaseInfo("db", "role_owner", "role_app", "v1"));

            public Task<Result<TenantDatabaseInfo>> EnsureDatabaseAsync(
                TenantDatabaseSpec spec,
                CancellationToken cancellationToken)
            {
                CreateCallCount++;
                return Task.FromResult(Result);
            }
        }

        private sealed class FakeSchemaInitializer : ITenantSchemaInitializer
        {
            public int InitializeCallCount { get; private set; }
            public Result Result { get; set; } =
                global::CafeHub.Application.Abstractions.Results.Result.Success();

            public Task<Result> InitializeAsync(
                TenantInitializationRequest request,
                CancellationToken cancellationToken)
            {
                InitializeCallCount++;
                return Task.FromResult(Result);
            }
        }

        private sealed class FakeUnitOfWork : IMasterUnitOfWork
        {
            public int BeginCount { get; private set; }
            public int CommitCount { get; private set; }
            public int RollbackCount { get; private set; }

            public Task BeginTransactionAsync(CancellationToken cancellationToken)
            {
                BeginCount++;
                return Task.CompletedTask;
            }

            public Task CommitTransactionAsync(CancellationToken cancellationToken)
            {
                CommitCount++;
                return Task.CompletedTask;
            }

            public Task RollbackTransactionAsync(CancellationToken cancellationToken)
            {
                RollbackCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeAuditWriter : IPlatformAuditWriter
        {
            public List<PlatformAuditEntry> Entries { get; } = [];

            public void Write(PlatformAuditEntry entry)
            {
                Entries.Add(entry);
            }
        }

        private sealed class FakeCurrentUser : ICurrentUser
        {
            public bool IsAuthenticated { get; set; } = true;
            public Guid? AdminId { get; set; } = Guid.CreateVersion7();
            public IReadOnlyCollection<string> Roles { get; set; } = new[] { "SuperAdmin" };
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            private DateTimeOffset _now;
            public FixedTimeProvider(DateTimeOffset now) => _now = now;
            public override DateTimeOffset GetUtcNow() => _now;
            public void Advance(TimeSpan span) => _now = _now.Add(span);
        }

        private readonly FakeJobStore _jobStore = new();
        private readonly FakeAttemptStore _attemptStore = new();
        private readonly FakeTenantStore _tenantStore = new();
        private readonly FakeTenantDatabaseStore _databaseStore = new();
        private readonly FakeOnboardingRequestStore _requestStore = new();
        private readonly FakeDatabaseCreator _databaseCreator = new();
        private readonly FakeSchemaInitializer _schemaInitializer = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeAuditWriter _auditWriter = new();
        private readonly FakeCurrentUser _currentUser = new();
        private readonly FixedTimeProvider _timeProvider = new(FixedNow);
        private ProvisioningRetryOptions _retryOptions = new();

        private ProvisioningOrchestrator CreateOrchestrator() =>
            new(
                _jobStore,
                _attemptStore,
                _tenantStore,
                _databaseStore,
                _requestStore,
                _databaseCreator,
                _schemaInitializer,
                _unitOfWork,
                _auditWriter,
                _currentUser,
                _timeProvider,
                _retryOptions);

        private static Tenant CreateTenant(TenantLifecycleStatus status = TenantLifecycleStatus.Provisioning)
        {
            return Tenant.Create(
                TenantId,
                RequestId,
                "my-cafe",
                status,
                FixedNow);
        }

        private static OnboardingRequest CreateRequest()
        {
            return OnboardingRequest.CreateForPublicRegistration(
                RequestId,
                Guid.CreateVersion7(),
                "owner@example.com",
                "Owner Name",
                null,
                "Cafe Name",
                "hash",
                "my-cafe",
                FixedNow);
        }

        private static ProvisioningJob CreateJobWithLease(
            TimeSpan? leaseDuration = null)
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, FixedNow);
            job.Claim("worker-1", FixedNow, leaseDuration ?? TimeSpan.FromSeconds(300));
            return job;
        }

        [Fact]
        public async Task HappyPath_RunsStepsInOrder_RecordsAttempts_MarksTenantReady()
        {
            _tenantStore.Tenant = CreateTenant();
            _jobStore.Job = CreateJobWithLease();
            _requestStore.Request = CreateRequest();

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            Assert.Equal(3, _attemptStore.Attempts.Count);
            Assert.All(_attemptStore.Attempts, a => Assert.Equal(ProvisioningJobAttemptStatus.Succeeded, a.Status));
            Assert.Equal(ProvisioningJobStatus.Succeeded, _jobStore.Job!.Status);
            Assert.Equal(TenantLifecycleStatus.Active, _tenantStore.Tenant!.LifecycleStatus);
            Assert.Equal(1, _unitOfWork.CommitCount);
            Assert.Single(_auditWriter.Entries);
            Assert.Equal("tenant.provisioned.succeeded", _auditWriter.Entries[0].Action);
            Assert.Equal(1, _databaseCreator.CreateCallCount);
            Assert.Equal(1, _schemaInitializer.InitializeCallCount);
        }

        [Fact]
        public async Task FailingStep_StopsLaterSteps_RecordsSanitizedFailure_Reschedules()
        {
            _tenantStore.Tenant = CreateTenant();
            _jobStore.Job = CreateJobWithLease();
            _requestStore.Request = CreateRequest();
            _databaseCreator.Result = Result.Failure<TenantDatabaseInfo>(ProvisioningErrors.EnsureDatabaseFailed());

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            Assert.Single(_attemptStore.Attempts);
            Assert.Equal(ProvisioningJobAttemptStatus.Failed, _attemptStore.Attempts[0].Status);
            Assert.NotEqual(ProvisioningJobStatus.Succeeded, _jobStore.Job!.Status);
            Assert.Equal(TenantLifecycleStatus.Provisioning, _tenantStore.Tenant!.LifecycleStatus);
            Assert.Equal(1, _databaseCreator.CreateCallCount);
            Assert.Equal(0, _schemaInitializer.InitializeCallCount);
        }

        [Fact]
        public async Task AlreadyActiveTenant_CompletesWithoutCallingCreatorOrInitializer()
        {
            _tenantStore.Tenant = CreateTenant(TenantLifecycleStatus.Active);
            _jobStore.Job = CreateJobWithLease();

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            Assert.Equal(ProvisioningJobStatus.Succeeded, _jobStore.Job!.Status);
            Assert.Equal(0, _databaseCreator.CreateCallCount);
            Assert.Equal(0, _schemaInitializer.InitializeCallCount);
        }

        [Fact]
        public async Task ExhaustedAttempts_JobIsFailed_NothingReady_NoFurtherAttempts()
        {
            _tenantStore.Tenant = CreateTenant();
            _jobStore.Job = CreateJobWithLease();
            _requestStore.Request = CreateRequest();
            _databaseCreator.Result = Result.Failure<TenantDatabaseInfo>(ProvisioningErrors.EnsureDatabaseFailed());
            _retryOptions = new ProvisioningRetryOptions { MaxAttempts = 1, BaseSeconds = 30, MaxSeconds = 900 };

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            Assert.Equal(ProvisioningJobStatus.Failed, _jobStore.Job!.Status);
            Assert.Equal(TenantLifecycleStatus.Provisioning, _tenantStore.Tenant!.LifecycleStatus);
            Assert.Single(_attemptStore.Attempts);
        }

        [Fact]
        public async Task FailureReason_DoesNotContainSecrets()
        {
            _tenantStore.Tenant = CreateTenant();
            _jobStore.Job = CreateJobWithLease();
            _requestStore.Request = CreateRequest();
            _schemaInitializer.Result = Result.Failure(ProvisioningErrors.SeedingFailed());

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            var failedEntry = _auditWriter.Entries.LastOrDefault(e => e.Action == "tenant.provisioning.failed");
            Assert.NotNull(failedEntry);
            Assert.DoesNotContain("hash", failedEntry.Reason!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", failedEntry.Reason!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("connection", failedEntry.Reason!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task ExpiredLease_AbortsWithoutMarkingReady()
        {
            _tenantStore.Tenant = CreateTenant();
            _jobStore.Job = CreateJobWithLease(TimeSpan.FromSeconds(1));
            _requestStore.Request = CreateRequest();
            _timeProvider.Advance(TimeSpan.FromSeconds(2));

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            Assert.NotEqual(TenantLifecycleStatus.Active, _tenantStore.Tenant!.LifecycleStatus);
        }

        [Fact]
        public async Task NullJob_ReturnsWithoutSideEffects()
        {
            _jobStore.Job = null;

            var orchestrator = CreateOrchestrator();
            await orchestrator.RunAsync(new ClaimedProvisioningJob(JobId, TenantId), CancellationToken.None);

            Assert.Equal(0, _databaseCreator.CreateCallCount);
            Assert.Equal(0, _schemaInitializer.InitializeCallCount);
            Assert.Empty(_attemptStore.Attempts);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(21)]
        [InlineData(-1)]
        public void RetryOptions_RejectsMaxAttemptsOutsideRange(int maxAttempts)
        {
            var options = new ProvisioningRetryOptions { MaxAttempts = maxAttempts };

            var problems = options.Validate();

            Assert.Contains(problems, p => p.Contains("MaxAttempts", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3601)]
        [InlineData(-1)]
        public void RetryOptions_RejectsBaseSecondsOutsideRange(int baseSeconds)
        {
            var options = new ProvisioningRetryOptions { BaseSeconds = baseSeconds };

            var problems = options.Validate();

            Assert.Contains(problems, p => p.Contains("BaseSeconds", StringComparison.Ordinal));
        }

        [Fact]
        public void RetryOptions_RejectsMaxSecondsBelowBaseSeconds()
        {
            var options = new ProvisioningRetryOptions { BaseSeconds = 100, MaxSeconds = 50 };

            var problems = options.Validate();

            Assert.Contains(problems, p => p.Contains("MaxSeconds", StringComparison.Ordinal));
        }

        [Fact]
        public void RetryOptions_AcceptsDefaults()
        {
            var options = new ProvisioningRetryOptions();

            var problems = options.Validate();

            Assert.Empty(problems);
        }
    }
}
