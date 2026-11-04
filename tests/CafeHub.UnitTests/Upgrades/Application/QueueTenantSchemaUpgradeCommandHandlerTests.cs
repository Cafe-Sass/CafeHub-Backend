using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Identity;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Commands;
using CafeHub.Application.Upgrades.Contracts;
using CafeHub.Application.Upgrades.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Domain.Tenants.Credentials;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Upgrades.Application
{
    public sealed class QueueTenantSchemaUpgradeCommandHandlerTests
    {
        private static readonly DateTimeOffset FixedNow = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        private static readonly Guid AdminId = Guid.CreateVersion7();
        private static readonly Guid TenantId = Guid.CreateVersion7();

        private sealed class FakeRunStore : ITenantSchemaUpgradeRunStore
        {
            public TenantSchemaUpgradeRun? ActiveRun { get; set; }

            public Task<TenantSchemaUpgradeRun?> FindByIdAsync(Guid runId, CancellationToken cancellationToken)
            {
                return Task.FromResult<TenantSchemaUpgradeRun?>(null);
            }

            public Task<TenantSchemaUpgradeRun?> FindActiveByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult(ActiveRun);
            }

            public Task AddAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task UpdateAsync(TenantSchemaUpgradeRun run, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeTenantStore : ITenantStore
        {
            public Tenant? Tenant { get; set; }

            public Task<Tenant?> FindByOnboardingRequestIdAsync(Guid onboardingRequestId, CancellationToken cancellationToken)
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

            public Task<TenantDatabase?> FindByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken)
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

        private sealed class FakeMigrationApplicator : ITenantMigrationApplicator
        {
            public string? LatestMigrationId { get; set; } = "latest";

            public Task ApplyAsync(string databaseName, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<string?> GetLatestAppliedMigrationIdAsync(string databaseName, CancellationToken cancellationToken)
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
            public Task<Result<string?>> UpgradeAsync(string databaseName, CancellationToken cancellationToken)
            {
                return Task.FromResult(Result.Success<string?>("applied"));
            }
        }

        private sealed class FakeUnitOfWork : IMasterUnitOfWork
        {
            public Task BeginTransactionAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task CommitTransactionAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task RollbackTransactionAsync(CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        private sealed class FakeAuditWriter : IPlatformAuditWriter
        {
            public void Write(PlatformAuditEntry entry)
            {
            }
        }

        private sealed class FakeCurrentUser : ICurrentUser
        {
            public bool IsAuthenticated { get; set; } = true;
            public Guid? AdminId { get; set; } = QueueTenantSchemaUpgradeCommandHandlerTests.AdminId;
            public IReadOnlyCollection<string> Roles { get; set; } = new[] { "SuperAdmin" };
        }

        private sealed class FixedTimeProvider : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => FixedNow;
        }

        private sealed class FakeLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        private readonly FakeRunStore _runStore = new();
        private readonly FakeTenantStore _tenantStore = new();
        private readonly FakeTenantDatabaseStore _databaseStore = new();
        private readonly FakeMigrationApplicator _migrationApplicator = new();
        private readonly FakeUpgrader _upgrader = new();
        private readonly FakeUnitOfWork _unitOfWork = new();
        private readonly FakeAuditWriter _auditWriter = new();
        private readonly FakeCurrentUser _currentUser = new();
        private readonly FixedTimeProvider _timeProvider = new();

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
                _timeProvider,
                new FakeLogger<TenantSchemaUpgradeService>());
        }

        private QueueTenantSchemaUpgradeCommandHandler CreateHandler()
        {
            return new QueueTenantSchemaUpgradeCommandHandler(
                CreateService(),
                _currentUser,
                _timeProvider,
                new FakeLogger<QueueTenantSchemaUpgradeCommandHandler>());
        }

        private static Tenant CreateActiveTenant()
        {
            return Tenant.Create(
                TenantId,
                Guid.CreateVersion7(),
                "my-cafe",
                TenantLifecycleStatus.Active,
                FixedNow);
        }

        private static TenantDatabase CreateProvisionedDatabase(Guid tenantId)
        {
            return TenantDatabase.Create(
                Guid.CreateVersion7(),
                tenantId,
                TenantDatabaseCredentialReference.CreateV1(tenantId),
                TenantDatabaseStatus.Provisioned,
                FixedNow);
        }

        [Fact]
        public async Task Handle_QueuesTheRunAndMapsTheResponse()
        {
            _tenantStore.Tenant = CreateActiveTenant();
            _databaseStore.Database = CreateProvisionedDatabase(TenantId);

            var result = await CreateHandler().Handle(
                new QueueTenantSchemaUpgradeCommand(TenantId),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = result.Value!;
            Assert.Equal(TenantId, response.TenantId);
            Assert.Equal("Pending", response.Status);
            Assert.Equal("latest", response.TargetMigrationId);
            Assert.Equal(AdminId, response.RequestedBy);
            Assert.Equal(FixedNow, response.CreatedAt);
        }

        [Fact]
        public async Task Handle_ReturnsTheQueueFailure_WhenTheTenantIsNotFound()
        {
            _tenantStore.Tenant = null;

            var result = await CreateHandler().Handle(
                new QueueTenantSchemaUpgradeCommand(TenantId),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.TenantNotFoundCode, result.Error!.Code);
        }

        [Fact]
        public async Task Handle_RejectsAnEmptyTenantId()
        {
            var result = await CreateHandler().Handle(
                new QueueTenantSchemaUpgradeCommand(Guid.Empty),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(TenantSchemaUpgradeErrors.InvalidTenantIdCode, result.Error!.Code);
        }

        [Fact]
        public async Task Handle_PassesTheCurrentAdminIdAsRequestedBy()
        {
            _tenantStore.Tenant = CreateActiveTenant();
            _databaseStore.Database = CreateProvisionedDatabase(TenantId);
            _currentUser.AdminId = AdminId;

            var result = await CreateHandler().Handle(
                new QueueTenantSchemaUpgradeCommand(TenantId),
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(AdminId, result.Value!.RequestedBy);
        }

        [Fact]
        public void TheHandler_RequiresAllDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => new QueueTenantSchemaUpgradeCommandHandler(null!, _currentUser, _timeProvider, new FakeLogger<QueueTenantSchemaUpgradeCommandHandler>()));
            Assert.Throws<ArgumentNullException>(() => new QueueTenantSchemaUpgradeCommandHandler(CreateService(), null!, _timeProvider, new FakeLogger<QueueTenantSchemaUpgradeCommandHandler>()));
            Assert.Throws<ArgumentNullException>(() => new QueueTenantSchemaUpgradeCommandHandler(CreateService(), _currentUser, null!, new FakeLogger<QueueTenantSchemaUpgradeCommandHandler>()));
            Assert.Throws<ArgumentNullException>(() => new QueueTenantSchemaUpgradeCommandHandler(CreateService(), _currentUser, _timeProvider, null!));
        }

        [Fact]
        public async Task Handle_WithANullCommand_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => CreateHandler().Handle(null!, CancellationToken.None));
        }
    }
}
