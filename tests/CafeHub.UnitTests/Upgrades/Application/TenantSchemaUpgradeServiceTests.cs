using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Domain.Tenants.Credentials;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Upgrades.Application
{
    public sealed class TenantSchemaUpgradeServiceTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid AdminId = Guid.CreateVersion7();

        private sealed class FakeRunStore : ITenantSchemaUpgradeRunStore
        {
            public TenantSchemaUpgradeRun? Run { get; set; }
            public TenantSchemaUpgradeRun? ActiveRun { get; set; }
            public List<TenantSchemaUpgradeRun> AddedRuns { get; } = [];
            public List<TenantSchemaUpgradeRun> UpdatedRuns { get; } = [];
            public int FindByIdCallCount { get; private set; }

            public Task<TenantSchemaUpgradeRun?> FindByIdAsync(
                Guid runId,
                CancellationToken cancellationToken)
            {
                FindByIdCallCount++;
                return Task.FromResult(Run);
            }

            public Task<TenantSchemaUpgradeRun?> FindActiveByTenantIdAsync(
                Guid tenantId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(ActiveRun);
            }

            public Task AddAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken)
            {
                AddedRuns.Add(run);
                return Task.CompletedTask;
            }

            public Task UpdateAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken)
            {
                UpdatedRuns.Add(run);
                return Task.CompletedTask;
            }
        }

        private sealed class FakeTenantStore : ITenantStore
        {
            public Tenant? Tenant { get; set; }

            public Task<Tenant?> FindByOnboardingRequestIdAsync(
                Guid onboardingRequestId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<Tenant?>(null);
            }

            public Task<Tenant?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult(Tenant);
            }

            public Task AddAsync(Tenant tenant, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeTenantDatabaseStore : ITenantDatabaseStore
        {
            public TenantDatabase? Database { get; set; }
            public int UpdateCallCount { get; private set; }

            public Task<TenantDatabase?> FindByTenantIdAsync(
                Guid tenantId,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(Database);
            }

            public Task AddAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken)
            {
                Database = tenantDatabase;
                return Task.CompletedTask;
            }

            public Task UpdateAsync(TenantDatabase tenantDatabase, CancellationToken cancellationToken)
            {
                Database = tenantDatabase;
                UpdateCallCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeMigrationApplicator : ITenantMigrationApplicator
        {
            public string? LatestMigrationId { get; set; } = "latest";

            public Task ApplyAsync(string databaseName, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<string?> GetLatestAppliedMigrationIdAsync(
                string databaseName,
                CancellationToken cancellationToken)
            {
                return Task.FromResult<string?>(null);
            }

            public Task<string?> GetLatestMigrationIdAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(LatestMigrationId);
            }
        }

        private sealed class FakeUpgrader : ITenantSchemaUpgrader
        {
            public Result<string?> UpgradeResult { get; set; } = Result.Success<string?>("applied");
            public int CallCount { get; private set; }
            public List<string> Databases { get; } = [];

            public Task<Result<string?>> UpgradeAsync(
                string databaseName,
                CancellationToken cancellationToken)
            {
                CallCount++;
                Databases.Add(databaseName);
                return Task.FromResult(UpgradeResult);
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

        private sealed class CapturingLogger : ILogger<TenantSchemaUpgradeService>
        {
            public List<(LogLevel Level, string Message)> Entries { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Entries.Add((logLevel, formatter(state, exception)));
            }
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => FixedNow;
        }

        private readonly FakeRunStore _runStore = new();
        private readonly FakeTenantStore _tenantStore = new();
        private readonly FakeTenantDatabaseStore _databaseStore = new();
        private readonly FakeMigrationApplicator _migrationApplicator = new();
        private readonly FakeUpgrader _upgrader = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeAuditWriter _auditWriter = new();
        private readonly CapturingLogger _logger = new();

        private TenantSchemaUpgradeService CreateService()
        {
            return new TenantSchemaUpgradeService(
                _runStore,
                _tenantStore,
                _databaseStore,
                _migrationApplicator,
                _upgrader,
                _unitOfWork,
                _auditWriter,
                new FixedTimeProvider(),
                _logger);
        }

        private static Tenant CreateTenant(TenantLifecycleStatus status = TenantLifecycleStatus.Active)
        {
            return Tenant.Create(
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "my-cafe",
                status,
                FixedNow);
        }

        private static TenantDatabase CreateDatabase(
            Guid tenantId,
            TenantDatabaseStatus status = TenantDatabaseStatus.Provisioned)
        {
            return TenantDatabase.Create(
                Guid.CreateVersion7(),
                tenantId,
                TenantDatabaseCredentialReference.CreateV1(tenantId),
                status,
                FixedNow);
        }

        private static TenantSchemaUpgradeRun CreateRunningRun(Guid tenantId)
        {
            var run = TenantSchemaUpgradeRun.Create(
                Guid.CreateVersion7(),
                tenantId,
                "latest",
                null,
                FixedNow);

            run.Claim("worker-1", FixedNow, TimeSpan.FromMinutes(5));
            return run;
        }

        [Fact]
        public async Task QueueAsync_RejectsAnEmptyTenantId()
        {
            var result = await CreateService().QueueAsync(Guid.Empty, null, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.InvalidTenantIdCode, result.Error!.Code);
            Assert.Empty(_runStore.AddedRuns);
        }

        [Fact]
        public async Task QueueAsync_ReturnsNotFound_WhenTheTenantDoesNotExist()
        {
            var result = await CreateService().QueueAsync(Guid.CreateVersion7(), null, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotFoundCode, result.Error!.Code);
        }

        [Fact]
        public async Task QueueAsync_ReturnsNotReady_WhenTheTenantIsNotActive()
        {
            _tenantStore.Tenant = CreateTenant(TenantLifecycleStatus.Suspended);

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, null, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotReadyCode, result.Error!.Code);
            Assert.Empty(_runStore.AddedRuns);
        }

        [Fact]
        public async Task QueueAsync_ReturnsNotReady_WhenTheTenantDatabaseIsMissing()
        {
            _tenantStore.Tenant = CreateTenant();

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, null, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotReadyCode, result.Error!.Code);
        }

        [Fact]
        public async Task QueueAsync_ReturnsNotReady_WhenTheTenantDatabaseIsNotProvisioned()
        {
            _tenantStore.Tenant = CreateTenant();
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id, TenantDatabaseStatus.Pending);

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, null, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotReadyCode, result.Error!.Code);
        }

        [Fact]
        public async Task QueueAsync_ReturnsConflict_WhenAnActiveRunExists()
        {
            _tenantStore.Tenant = CreateTenant();
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _runStore.ActiveRun = CreateRunningRun(_tenantStore.Tenant.Id);

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, AdminId, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.ActiveRunExistsCode, result.Error!.Code);
            Assert.Empty(_runStore.AddedRuns);
        }

        [Fact]
        public async Task QueueAsync_ReturnsConflict_WhenTheDatabaseAlreadyHasTheLatestMigration()
        {
            _tenantStore.Tenant = CreateTenant();
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _databaseStore.Database.RecordAppliedMigration("latest", FixedNow);

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, AdminId, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.AlreadyCurrentCode, result.Error!.Code);
            Assert.Empty(_runStore.AddedRuns);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task QueueAsync_ReturnsFailure_WhenNoTenantMigrationIsAvailable(string? latestMigrationId)
        {
            _tenantStore.Tenant = CreateTenant();
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _migrationApplicator.LatestMigrationId = latestMigrationId;

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, AdminId, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.NoMigrationAvailableCode, result.Error!.Code);
            Assert.Empty(_runStore.AddedRuns);
        }

        [Fact]
        public async Task QueueAsync_CreatesAPendingRunForTheLatestMigration()
        {
            _tenantStore.Tenant = CreateTenant();
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, AdminId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var run = result.Value!;
            Assert.NotEqual(Guid.Empty, run.Id);
            Assert.Equal(_tenantStore.Tenant.Id, run.TenantId);
            Assert.Equal("latest", run.TargetMigrationId);
            Assert.Equal(TenantSchemaUpgradeRunStatus.Pending, run.Status);
            Assert.Equal(AdminId, run.RequestedBy);
            Assert.Equal(AdminId, run.CreatedBy);
            Assert.Equal(AdminId, run.UpdatedBy);
            Assert.Equal(FixedNow, run.CreatedAt);
            Assert.Equal(FixedNow, run.UpdatedAt);
            Assert.Equal(0, run.AttemptCount);
            Assert.Null(run.FailureCode);
        }

        [Fact]
        public async Task QueueAsync_PersistsTheRunWithoutOpeningItsOwnTransaction()
        {
            _tenantStore.Tenant = CreateTenant();
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);

            var result = await CreateService().QueueAsync(_tenantStore.Tenant.Id, AdminId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Same(result.Value, Assert.Single(_runStore.AddedRuns));
            Assert.Equal(0, _unitOfWork.BeginCount);
            Assert.Equal(0, _unitOfWork.CommitCount);
        }

        [Fact]
        public async Task RunAsync_DoesNothing_WhenTheRunDoesNotExist()
        {
            var claimedRun = new ClaimedTenantSchemaUpgradeRun(Guid.CreateVersion7(), Guid.CreateVersion7());

            await CreateService().RunAsync(claimedRun, CancellationToken.None);

            Assert.Empty(_runStore.UpdatedRuns);
            Assert.Equal(0, _upgrader.CallCount);
            Assert.Equal(0, _unitOfWork.BeginCount);
        }

        [Fact]
        public async Task RunAsync_DoesNothing_WhenTheRunIsNotRunning()
        {
            var tenant = CreateTenant();
            var run = TenantSchemaUpgradeRun.Create(
                Guid.CreateVersion7(),
                tenant.Id,
                "latest",
                null,
                FixedNow);
            _runStore.Run = run;
            _tenantStore.Tenant = tenant;
            _databaseStore.Database = CreateDatabase(tenant.Id);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(run.Id, tenant.Id),
                CancellationToken.None);

            Assert.Empty(_runStore.UpdatedRuns);
            Assert.Equal(0, _upgrader.CallCount);
        }

        [Fact]
        public async Task RunAsync_DoesNothing_WhenTheClaimedTenantDoesNotMatchTheRun()
        {
            var tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(tenant.Id);
            _tenantStore.Tenant = tenant;
            _databaseStore.Database = CreateDatabase(tenant.Id);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, Guid.CreateVersion7()),
                CancellationToken.None);

            Assert.Empty(_runStore.UpdatedRuns);
            Assert.Equal(0, _upgrader.CallCount);
        }

        [Fact]
        public async Task RunAsync_FailsTheRunAsTenantNotReady_WhenTheTenantDoesNotExist()
        {
            var run = CreateRunningRun(Guid.CreateVersion7());
            _runStore.Run = run;

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(run.Id, run.TenantId),
                CancellationToken.None);

            Assert.Equal(TenantSchemaUpgradeRunStatus.Failed, run.Status);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.TenantNotReady, run.FailureCode);
            Assert.Equal(FixedNow, run.FinishedAt);
            Assert.Equal(0, _upgrader.CallCount);
            Assert.Single(_runStore.UpdatedRuns);
            Assert.Equal(1, _unitOfWork.BeginCount);
            Assert.Equal(1, _unitOfWork.CommitCount);
        }

        [Fact]
        public async Task RunAsync_FailsTheRunAsTenantNotReady_WhenTheTenantIsNotActive()
        {
            _tenantStore.Tenant = CreateTenant(TenantLifecycleStatus.Suspended);
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal(TenantSchemaUpgradeFailureCodes.TenantNotReady, _runStore.Run.FailureCode);
            Assert.Equal(0, _upgrader.CallCount);
        }

        [Fact]
        public async Task RunAsync_FailsTheRunAsTenantNotReady_WhenTheDatabaseIsMissing()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal(TenantSchemaUpgradeFailureCodes.TenantNotReady, _runStore.Run.FailureCode);
            Assert.Equal(0, _upgrader.CallCount);
        }

        [Fact]
        public async Task RunAsync_AppliesTheMigrationToTheTenantDatabase()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal("cafehub_tenant_my_cafe", Assert.Single(_upgrader.Databases));
        }

        [Fact]
        public async Task RunAsync_RecordsTheAppliedMigrationAndSucceedsTheRun()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _upgrader.UpgradeResult = Result.Success<string?>("applied-migration");

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal(TenantSchemaUpgradeRunStatus.Succeeded, _runStore.Run.Status);
            Assert.Equal(FixedNow, _runStore.Run.FinishedAt);
            Assert.Null(_runStore.Run.FailureCode);
            Assert.Equal("applied-migration", _databaseStore.Database!.SchemaMigrationId);
            Assert.Equal(1, _databaseStore.UpdateCallCount);
            Assert.Single(_runStore.UpdatedRuns);
            Assert.Equal(1, _unitOfWork.BeginCount);
            Assert.Equal(1, _unitOfWork.CommitCount);
            Assert.Equal(0, _unitOfWork.RollbackCount);
        }

        [Fact]
        public async Task RunAsync_FallsBackToTheTargetMigration_WhenTheAppliedVersionIsUnknown()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _upgrader.UpgradeResult = Result.Success<string?>(null);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal("latest", _databaseStore.Database!.SchemaMigrationId);
        }

        [Fact]
        public async Task RunAsync_FailsTheRunWithTheUpgraderFailureCode()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _upgrader.UpgradeResult = Result.Failure<string?>(TenantSchemaUpgradeErrors.MigrationFailed());

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal(TenantSchemaUpgradeRunStatus.Failed, _runStore.Run.Status);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.MigrationFailed, _runStore.Run.FailureCode);
            Assert.Null(_databaseStore.Database!.SchemaMigrationId);
            Assert.Equal(1, _unitOfWork.CommitCount);
        }

        [Fact]
        public async Task RunAsync_NormalizesAnUnknownFailureCodeToUnexpected()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _upgrader.UpgradeResult = Result.Failure<string?>(Error.Failure("Something.Weird", "Safe message."));

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            Assert.Equal(TenantSchemaUpgradeFailureCodes.Unexpected, _runStore.Run.FailureCode);
        }

        [Fact]
        public async Task RunAsync_WritesASuccessAuditWithOnlySafeMetadata()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            var entry = Assert.Single(_auditWriter.Entries);
            Assert.Equal(PlatformAuditActorKind.System, entry.ActorKind);
            Assert.Equal("tenant.schema_upgrade.succeeded", entry.Action);
            Assert.Equal(PlatformAuditOutcome.Succeeded, entry.Outcome);
            Assert.Equal("tenant_schema_upgrade_run", entry.TargetType);
            Assert.Equal(_runStore.Run.Id, entry.TargetId);
            Assert.Null(entry.Reason);
        }

        [Fact]
        public async Task RunAsync_WritesAFailureAuditWithASafeReason()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _upgrader.UpgradeResult = Result.Failure<string?>(TenantSchemaUpgradeErrors.ConnectionFailed());

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            var entry = Assert.Single(_auditWriter.Entries);
            Assert.Equal("tenant.schema_upgrade.failed", entry.Action);
            Assert.Equal(PlatformAuditOutcome.Failed, entry.Outcome);
            Assert.Equal("connection_failed", entry.Reason);
        }

        [Fact]
        public async Task RunAsync_LogsTheFailureCodeWithoutExceptionMaterial()
        {
            _tenantStore.Tenant = CreateTenant();
            _runStore.Run = CreateRunningRun(_tenantStore.Tenant.Id);
            _databaseStore.Database = CreateDatabase(_tenantStore.Tenant.Id);
            _upgrader.UpgradeResult = Result.Failure<string?>(TenantSchemaUpgradeErrors.MigrationFailed());

            await CreateService().RunAsync(
                new ClaimedTenantSchemaUpgradeRun(_runStore.Run.Id, _runStore.Run.TenantId),
                CancellationToken.None);

            var entry = Assert.Single(_logger.Entries);
            Assert.Equal(LogLevel.Warning, entry.Level);
            Assert.Contains(TenantSchemaUpgradeFailureCodes.MigrationFailed, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("connection", entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("password", entry.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
