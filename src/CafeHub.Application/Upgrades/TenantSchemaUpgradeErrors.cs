using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Upgrades
{
    public static class TenantSchemaUpgradeErrors
    {
        public const string InvalidTenantIdCode = "TenantSchemaUpgrade.InvalidTenantId";
        public const string InvalidTenantIdMessage = "The tenant id is required.";

        public const string TenantNotFoundCode = "TenantSchemaUpgrade.TenantNotFound";
        public const string TenantNotFoundMessage = "The tenant was not found.";

        public const string TenantNotReadyCode = "TenantSchemaUpgrade.TenantNotReady";
        public const string TenantNotReadyMessage = "The tenant is not ready for a schema upgrade.";

        public const string ActiveRunExistsCode = "TenantSchemaUpgrade.ActiveRunExists";
        public const string ActiveRunExistsMessage = "The tenant already has an active schema upgrade run.";

        public const string AlreadyCurrentCode = "TenantSchemaUpgrade.AlreadyCurrent";
        public const string AlreadyCurrentMessage = "The tenant database already has the latest schema migration.";

        public const string NoMigrationAvailableCode = "TenantSchemaUpgrade.NoMigrationAvailable";
        public const string NoMigrationAvailableMessage = "No tenant schema migration is available.";

        public const string InvalidWorkerIdCode = "TenantSchemaUpgrade.InvalidWorkerId";
        public const string InvalidWorkerIdMessage = "The worker id is required.";

        public const string InvalidLeaseDurationCode = "TenantSchemaUpgrade.InvalidLeaseDuration";
        public const string InvalidLeaseDurationMessage = "The lease duration must be positive.";

        public static Error InvalidTenantId()
        {
            return Error.Validation(InvalidTenantIdCode, InvalidTenantIdMessage);
        }

        public static Error TenantNotFound()
        {
            return Error.NotFound(TenantNotFoundCode, TenantNotFoundMessage);
        }

        public static Error TenantNotReady()
        {
            return Error.Conflict(TenantNotReadyCode, TenantNotReadyMessage);
        }

        public static Error ActiveRunExists()
        {
            return Error.Conflict(ActiveRunExistsCode, ActiveRunExistsMessage);
        }

        public static Error AlreadyCurrent()
        {
            return Error.Conflict(AlreadyCurrentCode, AlreadyCurrentMessage);
        }

        public static Error NoMigrationAvailable()
        {
            return Error.Failure(NoMigrationAvailableCode, NoMigrationAvailableMessage);
        }

        public static Error InvalidWorkerId()
        {
            return Error.Validation(InvalidWorkerIdCode, InvalidWorkerIdMessage);
        }

        public static Error InvalidLeaseDuration()
        {
            return Error.Validation(InvalidLeaseDurationCode, InvalidLeaseDurationMessage);
        }

        public static Error ConnectionFailed()
        {
            return Error.Failure(
                TenantSchemaUpgradeFailureCodes.ConnectionFailed,
                "The tenant database could not be reached.");
        }

        public static Error MigrationFailed()
        {
            return Error.Failure(
                TenantSchemaUpgradeFailureCodes.MigrationFailed,
                "The tenant schema migration could not be applied.");
        }

        public static Error Unexpected()
        {
            return Error.Failure(
                TenantSchemaUpgradeFailureCodes.Unexpected,
                "The tenant schema upgrade failed unexpectedly.");
        }
    }
}
