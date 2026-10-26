using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Provisioning;

namespace CafeHub.UnitTests.Provisioning.Domain
{
    public sealed class ProvisioningJobTests
    {
        private static readonly Guid JobId = Guid.CreateVersion7();
        private static readonly Guid TenantId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var createdAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, createdAt);

            Assert.Equal(JobId, job.Id);
            Assert.Equal(TenantId, job.TenantId);
            Assert.Equal(ProvisioningJobStatus.Pending, job.Status);
            Assert.Equal(createdAt, job.CreatedAt);
            Assert.Equal(createdAt, job.UpdatedAt);
            Assert.Null(job.CreatedBy);
            Assert.Null(job.UpdatedBy);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                ProvisioningJob.Create(Guid.Empty, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyTenantId()
        {
            Assert.Throws<ArgumentException>(() =>
                ProvisioningJob.Create(JobId, Guid.Empty, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Start_TransitionsPendingToRunning()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

            job.Start(now);

            Assert.Equal(ProvisioningJobStatus.Running, job.Status);
            Assert.Equal(now, job.UpdatedAt);
        }

        [Fact]
        public void Start_RejectsNonPendingState()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Succeeded, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<InvalidOperationException>(() => job.Start(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Complete_TransitionsRunningToSucceeded()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

            job.Complete(now);

            Assert.Equal(ProvisioningJobStatus.Succeeded, job.Status);
            Assert.Equal(now, job.UpdatedAt);
        }

        [Fact]
        public void Fail_TransitionsRunningToFailed()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

            job.Fail(now);

            Assert.Equal(ProvisioningJobStatus.Failed, job.Status);
            Assert.Equal(now, job.UpdatedAt);
        }

        [Fact]
        public void Claim_TransitionsPendingToRunningAndSetsLease()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero);

            job.Claim("worker-1", now, TimeSpan.FromMinutes(5));

            Assert.Equal(ProvisioningJobStatus.Running, job.Status);
            Assert.Equal("worker-1", job.LeasedBy);
            Assert.Equal(new DateTimeOffset(2026, 10, 21, 10, 10, 0, TimeSpan.Zero), job.LeaseExpiresAt);
            Assert.Equal(now, job.UpdatedAt);
        }

        [Fact]
        public void Claim_RejectsNonPendingState()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<InvalidOperationException>(() => job.Claim("worker-1", new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero), TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Claim_RejectsNotDueJob()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), dueAt: new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero));

            Assert.Throws<InvalidOperationException>(() => job.Claim("worker-1", new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero), TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Claim_RejectsActiveLease()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero);

            job.Claim("worker-1", now, TimeSpan.FromMinutes(5));

            Assert.Throws<InvalidOperationException>(() => job.Claim("worker-2", now.AddMinutes(1), TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Claim_AllowsReclaimAfterLeaseExpiry()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero);

            job.Claim("worker-1", now, TimeSpan.FromMinutes(5));
            job.Claim("worker-2", now.AddMinutes(10), TimeSpan.FromMinutes(5));

            Assert.Equal("worker-2", job.LeasedBy);
            Assert.Equal(ProvisioningJobStatus.Running, job.Status);
        }

        [Fact]
        public void Claim_RejectsEmptyWorkerId()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentException>(() => job.Claim("", new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero), TimeSpan.FromMinutes(5)));
        }

        [Fact]
        public void Claim_RejectsNonPositiveLeaseDuration()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<ArgumentOutOfRangeException>(() => job.Claim("worker-1", new DateTimeOffset(2026, 10, 21, 10, 5, 0, TimeSpan.Zero), TimeSpan.Zero));
        }

        [Fact]
        public void Retry_TransitionsFailedToPending()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Failed, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);

            job.Retry(now);

            Assert.Equal(ProvisioningJobStatus.Pending, job.Status);
            Assert.Equal(now, job.UpdatedAt);
        }

        [Fact]
        public void Retry_RejectsNonFailedState()
        {
            var job = ProvisioningJob.Create(JobId, TenantId, ProvisioningJobStatus.Pending, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<InvalidOperationException>(() => job.Retry(new DateTimeOffset(2026, 10, 22, 10, 0, 0, TimeSpan.Zero)));
        }
    }

    public sealed class ProvisioningJobAttemptTests
    {
        private static readonly Guid AttemptId = Guid.CreateVersion7();
        private static readonly Guid JobId = Guid.CreateVersion7();

        [Fact]
        public void Create_SetsAllProperties()
        {
            var startedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);
            var finishedAt = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);

            var attempt = ProvisioningJobAttempt.Create(AttemptId, JobId, 1, ProvisioningJobAttemptStatus.Succeeded, startedAt, finishedAt, null, startedAt);

            Assert.Equal(AttemptId, attempt.Id);
            Assert.Equal(JobId, attempt.ProvisioningJobId);
            Assert.Equal(1, attempt.AttemptNumber);
            Assert.Equal(ProvisioningJobAttemptStatus.Succeeded, attempt.Status);
            Assert.Equal(startedAt, attempt.StartedAt);
            Assert.Equal(finishedAt, attempt.FinishedAt);
            Assert.Null(attempt.ErrorCode);
            Assert.Equal(startedAt, attempt.CreatedAt);
            Assert.Equal(startedAt, attempt.UpdatedAt);
        }

        [Fact]
        public void Create_RejectsEmptyId()
        {
            Assert.Throws<ArgumentException>(() =>
                ProvisioningJobAttempt.Create(Guid.Empty, JobId, 1, ProvisioningJobAttemptStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsEmptyJobId()
        {
            Assert.Throws<ArgumentException>(() =>
                ProvisioningJobAttempt.Create(AttemptId, Guid.Empty, 1, ProvisioningJobAttemptStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsAttemptNumberLessThanOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProvisioningJobAttempt.Create(AttemptId, JobId, 0, ProvisioningJobAttemptStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Create_RejectsErrorCodeOver128Chars()
        {
            var longCode = new string('x', 129);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ProvisioningJobAttempt.Create(AttemptId, JobId, 1, ProvisioningJobAttemptStatus.Failed, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), null, longCode, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero)));
        }

        [Fact]
        public void Complete_TransitionsRunningToSucceeded()
        {
            var attempt = ProvisioningJobAttempt.Create(AttemptId, JobId, 1, ProvisioningJobAttemptStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);

            attempt.Complete(now, null);

            Assert.Equal(ProvisioningJobAttemptStatus.Succeeded, attempt.Status);
            Assert.Equal(now, attempt.FinishedAt);
            Assert.Equal(now, attempt.UpdatedAt);
            Assert.Null(attempt.ErrorCode);
        }

        [Fact]
        public void Fail_TransitionsRunningToFailed()
        {
            var attempt = ProvisioningJobAttempt.Create(AttemptId, JobId, 1, ProvisioningJobAttemptStatus.Running, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), null, null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));
            var now = new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero);

            attempt.Fail(now, "timeout");

            Assert.Equal(ProvisioningJobAttemptStatus.Failed, attempt.Status);
            Assert.Equal(now, attempt.FinishedAt);
            Assert.Equal("timeout", attempt.ErrorCode);
            Assert.Equal(now, attempt.UpdatedAt);
        }

        [Fact]
        public void Complete_RejectsNonRunningState()
        {
            var attempt = ProvisioningJobAttempt.Create(AttemptId, JobId, 1, ProvisioningJobAttemptStatus.Succeeded, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 21, 11, 0, 0, TimeSpan.Zero), null, new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero));

            Assert.Throws<InvalidOperationException>(() => attempt.Complete(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), null));
        }
    }

    public sealed class SanitizedFailureTests
    {
        [Fact]
        public void Create_ReturnsEmptyForNull()
        {
            var result = SanitizedFailure.Create(null);

            Assert.Equal(string.Empty, result.Value);
        }

        [Fact]
        public void Create_ReturnsEmptyForWhitespace()
        {
            var result = SanitizedFailure.Create("   ");

            Assert.Equal(string.Empty, result.Value);
        }

        [Fact]
        public void Create_ReturnsValueUnchangedForSafeShortMessage()
        {
            var result = SanitizedFailure.Create("provisioning failed");

            Assert.Equal("provisioning failed", result.Value);
        }

        [Fact]
        public void Create_TruncatesTo128Characters()
        {
            var longMessage = new string('a', 200);

            var result = SanitizedFailure.Create(longMessage);

            Assert.Equal(128, result.Value.Length);
            Assert.Equal(new string('a', 128), result.Value);
        }

        [Theory]
        [InlineData("Password=secret")]
        [InlineData("Host=localhost")]
        [InlineData("postgres://user:pass@host/db")]
        [InlineData("password=secret")]
        [InlineData("HOST=localhost")]
        public void Create_ReplacesConnectionStringMarkerWithGenericMessage(string unsafeMessage)
        {
            var result = SanitizedFailure.Create(unsafeMessage);

            Assert.Equal("Provisioning failed.", result.Value);
            Assert.NotEqual(unsafeMessage, result.Value);
        }

        [Fact]
        public void Create_ReturnsSafeMessageWithoutEchoingOriginal()
        {
            var unsafeMessage = "Password=mysecret123 Host=db.example.com";

            var result = SanitizedFailure.Create(unsafeMessage);

            Assert.DoesNotContain("mysecret123", result.Value);
            Assert.DoesNotContain("db.example.com", result.Value);
            Assert.Equal("Provisioning failed.", result.Value);
        }
    }
}
