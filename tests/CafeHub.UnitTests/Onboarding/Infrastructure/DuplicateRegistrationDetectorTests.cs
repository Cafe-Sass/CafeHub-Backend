using CafeHub.Infrastructure.Persistence.Master.Onboarding;
using Npgsql;

namespace CafeHub.UnitTests.Onboarding.Infrastructure
{
    public sealed class DuplicateRegistrationDetectorTests
    {
        [Fact]
        public void Matches_RecognizesGuardConstraint()
        {
            Assert.True(DuplicateRegistrationDetector.Matches(
                "23505", "uq_onboarding_requests_public_owner_email", "uq_onboarding_requests_public_owner_email"));
        }

        [Fact]
        public void Matches_RecognizesSlugConstraint()
        {
            Assert.True(DuplicateRegistrationDetector.Matches(
                "23505", "uq_onboarding_requests_slug", "uq_onboarding_requests_slug"));
        }

        [Fact]
        public void Matches_RejectsOtherUniqueViolation()
        {
            Assert.False(DuplicateRegistrationDetector.Matches(
                "23505", "uq_tenants_slug", "uq_onboarding_requests_public_owner_email"));
            Assert.False(DuplicateRegistrationDetector.Matches(
                "23505", "uq_tenants_slug", "uq_onboarding_requests_slug"));
        }

        [Fact]
        public void Matches_RejectsNonUniqueSqlState()
        {
            Assert.False(DuplicateRegistrationDetector.Matches(
                "08001", "uq_onboarding_requests_public_owner_email", "uq_onboarding_requests_public_owner_email"));
        }

        [Fact]
        public void Matches_RejectsNullConstraintName()
        {
            Assert.False(DuplicateRegistrationDetector.Matches(
                "23505", null, "uq_onboarding_requests_public_owner_email"));
        }

        [Fact]
        public void IsPublicEmailGuardViolation_RejectsNonPostgresException()
        {
            var detector = new DuplicateRegistrationDetector();
            var exception = new InvalidOperationException("boom");

            Assert.False(detector.IsPublicEmailGuardViolation(exception));
            Assert.False(detector.IsSlugTakenViolation(exception));
        }

        [Fact]
        public void IsSlugTakenViolation_RejectsNonUniquePostgresError()
        {
            var detector = new DuplicateRegistrationDetector();
            var exception = new PostgresException("connection failed", "08001", "ERROR", "Routine");

            Assert.False(detector.IsPublicEmailGuardViolation(exception));
            Assert.False(detector.IsSlugTakenViolation(exception));
        }
    }
}
