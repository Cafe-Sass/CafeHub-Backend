using CafeHub.Application.Onboarding.Contracts;
using Npgsql;

namespace CafeHub.Infrastructure.Persistence.Master.Onboarding
{
    internal sealed class DuplicateRegistrationDetector : IDuplicateRegistrationDetector
    {
        private const string UniqueViolationSqlState = "23505";

        public bool IsPublicEmailGuardViolation(Exception exception)
        {
            return Classify(exception, "uq_onboarding_requests_public_owner_email");
        }

        public bool IsSlugTakenViolation(Exception exception)
        {
            return Classify(exception, "uq_onboarding_requests_slug");
        }

        /// <summary>
        /// Matches a database exception against a constraint. Extracted as a static member so it can be tested
        /// without constructing an Npgsql exception (whose provider-populated properties are hard to arrange).
        /// </summary>
        public static bool Matches(string? sqlState, string? constraintName, string expectedConstraintName)
        {
            return sqlState == UniqueViolationSqlState
                && string.Equals(constraintName, expectedConstraintName, StringComparison.Ordinal);
        }

        private static bool Classify(Exception exception, string constraintName)
        {
            if (exception is not PostgresException postgres)
            {
                return false;
            }

            return Matches(postgres.SqlState, postgres.ConstraintName, constraintName);
        }
    }
}
