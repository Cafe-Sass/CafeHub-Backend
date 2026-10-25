namespace CafeHub.Domain.Provisioning
{
    public sealed class ProvisioningJobAttempt
    {
        private ProvisioningJobAttempt()
        {
        }

        public Guid Id { get; private set; }

        public Guid ProvisioningJobId { get; private set; }

        public int AttemptNumber { get; private set; }

        public ProvisioningJobAttemptStatus Status { get; private set; }

        public DateTimeOffset StartedAt { get; private set; }

        public DateTimeOffset? FinishedAt { get; private set; }

        public string? ErrorCode { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public static ProvisioningJobAttempt Create(
            Guid id,
            Guid provisioningJobId,
            int attemptNumber,
            ProvisioningJobAttemptStatus status,
            DateTimeOffset startedAt,
            DateTimeOffset? finishedAt,
            string? errorCode,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The provisioning job attempt id is required.", nameof(id));
            }

            if (provisioningJobId == Guid.Empty)
            {
                throw new ArgumentException("The provisioning job id is required.", nameof(provisioningJobId));
            }

            if (attemptNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptNumber),
                    "The attempt number must be at least 1.");
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("The provisioning job attempt status is not defined.", nameof(status));
            }

            if (errorCode is not null && errorCode.Length > 128)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(errorCode),
                    "The error code must not exceed 128 characters.");
            }

            return new ProvisioningJobAttempt
            {
                Id = id,
                ProvisioningJobId = provisioningJobId,
                AttemptNumber = attemptNumber,
                Status = status,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                ErrorCode = errorCode,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
            };
        }

        public void Complete(DateTimeOffset now, string? errorCode = null)
        {
            if (Status != ProvisioningJobAttemptStatus.Running)
            {
                throw new InvalidOperationException("A provisioning job attempt can only be completed from Running state.");
            }

            Status = ProvisioningJobAttemptStatus.Succeeded;
            FinishedAt = now;
            ErrorCode = errorCode;
            UpdatedAt = now;
        }

        public void Fail(DateTimeOffset now, string? errorCode = null)
        {
            if (Status != ProvisioningJobAttemptStatus.Running)
            {
                throw new InvalidOperationException("A provisioning job attempt can only fail from Running state.");
            }

            Status = ProvisioningJobAttemptStatus.Failed;
            FinishedAt = now;
            ErrorCode = errorCode;
            UpdatedAt = now;
        }
    }
}
