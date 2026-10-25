using CafeHub.Application.Onboarding.Contracts;
using Npgsql;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class DuplicateRegistrationDetector : IDuplicateRegistrationDetector
    {
        private const string UniqueViolationSqlState = "23505";

        public bool IsPublicEmailGuardViolation(Exception exception)
        {
            return IsUniqueViolation(exception, "uq_onboarding_requests_public_owner_email");
        }

        public bool IsSlugTakenViolation(Exception exception)
        {
            return IsUniqueViolation(exception, "uq_onboarding_requests_slug");
        }

        private static bool IsUniqueViolation(Exception exception, string constraintName)
        {
            return exception is PostgresException postgres
                && postgres.SqlState == UniqueViolationSqlState
                && string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
