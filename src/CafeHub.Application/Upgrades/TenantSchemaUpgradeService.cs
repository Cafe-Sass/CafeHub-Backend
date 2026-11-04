using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Onboarding.Contracts;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Upgrades.Contracts;
using CafeHub.Domain.Tenants;
using Microsoft.Extensions.Logging;

namespace CafeHub.Application.Upgrades
{
    public sealed class TenantSchemaUpgradeService : ITenantSchemaUpgradeRunner
    {
        private const string SuccessAction = "tenant.schema_upgrade.succeeded";
        private const string FailureAction = "tenant.schema_upgrade.failed";
        private const string TargetType = "tenant_schema_upgrade_run";

        private readonly ITenantSchemaUpgradeRunStore runStore;
        private readonly ITenantStore tenantStore;
        private readonly ITenantDatabaseStore tenantDatabaseStore;
        private readonly ITenantMigrationApplicator migrationApplicator;
        private readonly ITenantSchemaUpgrader upgrader;
        private readonly IMasterUnitOfWork unitOfWork;
        private readonly IPlatformAuditWriter auditWriter;
        private readonly TimeProvider timeProvider;
        private readonly ILogger<TenantSchemaUpgradeService> logger;

        public TenantSchemaUpgradeService(
            ITenantSchemaUpgradeRunStore runStore,
            ITenantStore tenantStore,
            ITenantDatabaseStore tenantDatabaseStore,
            ITenantMigrationApplicator migrationApplicator,
            ITenantSchemaUpgrader upgrader,
            IMasterUnitOfWork unitOfWork,
            IPlatformAuditWriter auditWriter,
            TimeProvider timeProvider,
            ILogger<TenantSchemaUpgradeService> logger)
        {
            this.runStore = runStore;
            this.tenantStore = tenantStore;
            this.tenantDatabaseStore = tenantDatabaseStore;
            this.migrationApplicator = migrationApplicator;
            this.upgrader = upgrader;
            this.unitOfWork = unitOfWork;
            this.auditWriter = auditWriter;
            this.timeProvider = timeProvider;
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<TenantSchemaUpgradeRun>> QueueAsync(
            Guid tenantId,
            Guid? requestedBy,
            CancellationToken cancellationToken)
        {
            if (tenantId == Guid.Empty)
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.InvalidTenantId());
            }

            var tenant = await tenantStore.FindByIdAsync(tenantId, cancellationToken);
            if (tenant is null)
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.TenantNotFound());
            }

            if (tenant.LifecycleStatus != TenantLifecycleStatus.Active)
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.TenantNotReady());
            }

            var database = await tenantDatabaseStore.FindByTenantIdAsync(tenantId, cancellationToken);
            if (database is null || database.Status != TenantDatabaseStatus.Provisioned)
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.TenantNotReady());
            }

            var activeRun = await runStore.FindActiveByTenantIdAsync(tenantId, cancellationToken);
            if (activeRun is not null)
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.ActiveRunExists());
            }

            var targetMigrationId = await migrationApplicator.GetLatestMigrationIdAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(targetMigrationId))
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.NoMigrationAvailable());
            }

            if (string.Equals(database.SchemaMigrationId, targetMigrationId, StringComparison.Ordinal))
            {
                return Result.Failure<TenantSchemaUpgradeRun>(TenantSchemaUpgradeErrors.AlreadyCurrent());
            }

            var now = timeProvider.GetUtcNow();
            var run = TenantSchemaUpgradeRun.Create(
                Guid.CreateVersion7(),
                tenantId,
                targetMigrationId,
                requestedBy,
                now,
                requestedBy);

            await runStore.AddAsync(run, cancellationToken);

            return Result.Success(run);
        }

        public async Task RunAsync(
            ClaimedTenantSchemaUpgradeRun claimedRun,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(claimedRun);

            var run = await runStore.FindByIdAsync(claimedRun.RunId, cancellationToken);
            if (run is null ||
                run.TenantId != claimedRun.TenantId ||
                run.Status != TenantSchemaUpgradeRunStatus.Running)
            {
                return;
            }

            var tenant = await tenantStore.FindByIdAsync(run.TenantId, cancellationToken);
            var database = await tenantDatabaseStore.FindByTenantIdAsync(run.TenantId, cancellationToken);

            if (tenant is null ||
                tenant.LifecycleStatus != TenantLifecycleStatus.Active ||
                database is null ||
                database.Status != TenantDatabaseStatus.Provisioned)
            {
                await FailAsync(run, TenantSchemaUpgradeFailureCodes.TenantNotReady, cancellationToken);
                return;
            }

            var databaseName = TenantDatabaseNaming.DatabaseName(tenant.Slug);
            var upgradeResult = await upgrader.UpgradeAsync(databaseName, cancellationToken);

            if (upgradeResult.IsFailure)
            {
                var failureCode = TenantSchemaUpgradeFailureCodes.IsDefined(upgradeResult.Error!.Code)
                    ? upgradeResult.Error.Code
                    : TenantSchemaUpgradeFailureCodes.Unexpected;

                await FailAsync(run, failureCode, cancellationToken);
                return;
            }

            var appliedMigrationId = string.IsNullOrWhiteSpace(upgradeResult.Value)
                ? run.TargetMigrationId
                : upgradeResult.Value;

            var now = timeProvider.GetUtcNow();
            database.RecordAppliedMigration(appliedMigrationId, now);
            run.Succeed(now);

            await CompleteAsync(run, database, cancellationToken);
        }

        private async Task CompleteAsync(
            TenantSchemaUpgradeRun run,
            TenantDatabase database,
            CancellationToken cancellationToken)
        {
            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await runStore.UpdateAsync(run, cancellationToken);
                await tenantDatabaseStore.UpdateAsync(database, cancellationToken);

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = SuccessAction,
                    TargetType = TargetType,
                    TargetId = run.Id,
                    Outcome = PlatformAuditOutcome.Succeeded,
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        private async Task FailAsync(
            TenantSchemaUpgradeRun run,
            string failureCode,
            CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();
            run.Fail(now, failureCode);

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await runStore.UpdateAsync(run, cancellationToken);

                auditWriter.Write(new PlatformAuditEntry
                {
                    ActorKind = PlatformAuditActorKind.System,
                    Action = FailureAction,
                    TargetType = TargetType,
                    TargetId = run.Id,
                    Outcome = PlatformAuditOutcome.Failed,
                    Reason = FailureReason(failureCode),
                });

                await unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            logger.LogWarning(
                "Tenant schema upgrade run {RunId} failed with {FailureCode}.",
                run.Id,
                failureCode);
        }

        private static string FailureReason(string failureCode)
        {
            return failureCode switch
            {
                TenantSchemaUpgradeFailureCodes.TenantNotReady => "tenant_not_ready",
                TenantSchemaUpgradeFailureCodes.ConnectionFailed => "connection_failed",
                TenantSchemaUpgradeFailureCodes.MigrationFailed => "migration_failed",
                _ => "unexpected",
            };
        }
    }
}
