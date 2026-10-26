using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Provisioning
{
    public static class ProvisioningErrors
    {
        public const string InvalidWorkerIdCode = "Provisioning.InvalidWorkerId";
        public const string InvalidWorkerIdMessage = "The worker id is required.";

        public const string InvalidLeaseDurationCode = "Provisioning.InvalidLeaseDuration";
        public const string InvalidLeaseDurationMessage = "The lease duration must be positive.";

        public const string InvalidTenantIdCode = "Provisioning.InvalidTenantId";
        public const string InvalidTenantIdMessage = "The tenant id is required.";

        public const string InvalidTenantSlugCode = "Provisioning.InvalidTenantSlug";
        public const string InvalidTenantSlugMessage = "The tenant slug is not valid.";

        public const string EnsureDatabaseFailedCode = "Provisioning.EnsureDatabaseFailed";
        public const string EnsureDatabaseFailedMessage = "The tenant database could not be ensured.";

        public const string MigrationFailedCode = "Provisioning.MigrationFailed";
        public const string MigrationFailedMessage = "The tenant migration could not be applied.";

        public const string SeedingFailedCode = "Provisioning.SeedingFailed";
        public const string SeedingFailedMessage = "The tenant seed data could not be written.";

        public static Error InvalidWorkerId()
        {
            return Error.Validation(InvalidWorkerIdCode, InvalidWorkerIdMessage);
        }

        public static Error InvalidLeaseDuration()
        {
            return Error.Validation(InvalidLeaseDurationCode, InvalidLeaseDurationMessage);
        }

        public static Error InvalidTenantId()
        {
            return Error.Validation(InvalidTenantIdCode, InvalidTenantIdMessage);
        }

        public static Error InvalidTenantSlug()
        {
            return Error.Validation(InvalidTenantSlugCode, InvalidTenantSlugMessage);
        }

        public static Error EnsureDatabaseFailed()
        {
            return Error.Failure(EnsureDatabaseFailedCode, EnsureDatabaseFailedMessage);
        }

        public static Error MigrationFailed()
        {
            return Error.Failure(MigrationFailedCode, MigrationFailedMessage);
        }

        public static Error SeedingFailed()
        {
            return Error.Failure(SeedingFailedCode, SeedingFailedMessage);
        }
    }
}
