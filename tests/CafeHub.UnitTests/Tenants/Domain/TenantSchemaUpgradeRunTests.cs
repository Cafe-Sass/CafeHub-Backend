using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Tenants.Domain
{
    public sealed class TenantSchemaUpgradeRunTests
    {
        private static readonly Guid RunId = Guid.CreateVersion7();
        private static readonly Guid TenantId = Guid.CreateVersion7();
        private static readonly Guid AdminId = Guid.CreateVersion7();
        private static readonly DateTimeOffset CreatedAt = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset Now = CreatedAt.AddMinutes(1);

        private static TenantSchemaUpgradeRun CreateRun()
        {
            return TenantSchemaUpgradeRun.Create(
                RunId,
                TenantId,
                "20261104120000_AddTenantSchemaUpgradeTracking",
                AdminId,
                CreatedAt,
                AdminId);
        }

        [Fact]
        public void Create_StartsPendingWithoutAttemptsOrFailure()
        {
            var run = CreateRun();

            Assert.Equal(RunId, run.Id);
            Assert.Equal(TenantId, run.TenantId);
            Assert.Equal("20261104120000_AddTenantSchemaUpgradeTracking", run.TargetMigrationId);
            Assert.Equal(TenantSchemaUpgradeRunStatus.Pending, run.Status);
            Assert.Null(run.FailureCode);
            Assert.Equal(0, run.AttemptCount);
            Assert.Equal(AdminId, run.RequestedBy);
            Assert.Null(run.StartedAt);
            Assert.Null(run.FinishedAt);
            Assert.Null(run.LeaseExpiresAt);
            Assert.Null(run.LeasedBy);
            Assert.Equal(CreatedAt, run.CreatedAt);
            Assert.Equal(CreatedAt, run.UpdatedAt);
            Assert.Equal(AdminId, run.CreatedBy);
            Assert.Equal(AdminId, run.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyIds()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantSchemaUpgradeRun.Create(
                    Guid.Empty,
                    TenantId,
                    "20261104120000_AddTenantSchemaUpgradeTracking",
                    null,
                    CreatedAt));
            Assert.Throws<ArgumentException>(() =>
                TenantSchemaUpgradeRun.Create(
                    RunId,
                    Guid.Empty,
                    "20261104120000_AddTenantSchemaUpgradeTracking",
                    null,
                    CreatedAt));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Create_RejectsBlankTargetMigrationIds(string? migrationId)
        {
            Assert.Throws<ArgumentException>(() =>
                TenantSchemaUpgradeRun.Create(RunId, TenantId, migrationId!, null, CreatedAt));
        }

        [Fact]
        public void Create_RejectsOverlyLongTargetMigrationIds()
        {
            Assert.Throws<ArgumentException>(() =>
                TenantSchemaUpgradeRun.Create(RunId, TenantId, new string('a', 151), null, CreatedAt));
        }

        [Fact]
        public void Claim_MovesPendingToRunningAndIncrementsTheAttemptCount()
        {
            var run = CreateRun();

            run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

            Assert.Equal(TenantSchemaUpgradeRunStatus.Running, run.Status);
            Assert.Equal(1, run.AttemptCount);
            Assert.Equal(Now, run.StartedAt);
            Assert.Equal("worker-1", run.LeasedBy);
            Assert.Equal(Now.AddMinutes(5), run.LeaseExpiresAt);
            Assert.Equal(Now, run.UpdatedAt);
        }

        [Fact]
        public void Claim_RejectsALiveLease()
        {
            var run = CreateRun();
            run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

            Assert.Throws<InvalidOperationException>(() =>
                run.Claim("worker-2", Now.AddMinutes(1), TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Claim_ReclaimsAnExpiredLeaseWithoutDecreasingTheAttemptCount()
        {
            var run = CreateRun();
            run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

            run.Claim("worker-2", Now.AddMinutes(6), TimeSpan.FromMinutes(5));

            Assert.Equal(TenantSchemaUpgradeRunStatus.Running, run.Status);
            Assert.Equal(2, run.AttemptCount);
            Assert.Equal(Now, run.StartedAt);
            Assert.Equal("worker-2", run.LeasedBy);
            Assert.Equal(Now.AddMinutes(11), run.LeaseExpiresAt);
        }

        [Fact]
        public void Claim_RejectsANonPositiveLeaseDuration()
        {
            var run = CreateRun();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                run.Claim("worker-1", Now, TimeSpan.Zero));
        }

        [Fact]
        public void Claim_RejectsABlankWorkerId()
        {
            var run = CreateRun();

            Assert.Throws<ArgumentException>(() =>
                run.Claim(" ", Now, TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Succeed_MovesRunningToSucceededAndSetsTheFinishTime()
        {
            var run = CreateRun();
            run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

            run.Succeed(Now.AddMinutes(1));

            Assert.Equal(TenantSchemaUpgradeRunStatus.Succeeded, run.Status);
            Assert.Equal(Now.AddMinutes(1), run.FinishedAt);
            Assert.Null(run.LeasedBy);
            Assert.Null(run.LeaseExpiresAt);
        }

        [Fact]
        public void Succeed_RejectsPendingRuns()
        {
            var run = CreateRun();

            Assert.Throws<InvalidOperationException>(() => run.Succeed(Now));
        }

        [Fact]
        public void Fail_MovesRunningToFailedAndSetsTheClosedFailureCode()
        {
            var run = CreateRun();
            run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

            run.Fail(Now.AddMinutes(1), TenantSchemaUpgradeFailureCodes.MigrationFailed);

            Assert.Equal(TenantSchemaUpgradeRunStatus.Failed, run.Status);
            Assert.Equal(TenantSchemaUpgradeFailureCodes.MigrationFailed, run.FailureCode);
            Assert.Equal(Now.AddMinutes(1), run.FinishedAt);
            Assert.Null(run.LeasedBy);
            Assert.Null(run.LeaseExpiresAt);
        }

        [Fact]
        public void Fail_RejectsAnUnknownFailureCode()
        {
            var run = CreateRun();
            run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

            Assert.Throws<ArgumentException>(() =>
                run.Fail(Now.AddMinutes(1), "Upgrade.NotAClosedCode"));
        }

        [Fact]
        public void Fail_RejectsPendingRuns()
        {
            var run = CreateRun();

            Assert.Throws<InvalidOperationException>(() =>
                run.Fail(Now, TenantSchemaUpgradeFailureCodes.Unexpected));
        }

        [Fact]
        public void AFinishedRun_CannotMoveAgain()
        {
            var successfulRun = CreateRun();
            successfulRun.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
            successfulRun.Succeed(Now.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                successfulRun.Fail(Now.AddMinutes(2), TenantSchemaUpgradeFailureCodes.Unexpected));

            var failedRun = CreateRun();
            failedRun.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
            failedRun.Fail(Now.AddMinutes(1), TenantSchemaUpgradeFailureCodes.Unexpected);

            Assert.Throws<InvalidOperationException>(() => failedRun.Succeed(Now.AddMinutes(2)));
        }

        [Fact]
        public void FailureCodes_ContainOnlyTheClosedUpgradeList()
        {
            Assert.Equal(
                new[]
                {
                    "Upgrade.ConnectionFailed",
                    "Upgrade.MigrationFailed",
                    "Upgrade.TenantNotReady",
                    "Upgrade.Unexpected",
                },
                TenantSchemaUpgradeFailureCodes.All.OrderBy(code => code, StringComparer.Ordinal));
        }
    }
}
