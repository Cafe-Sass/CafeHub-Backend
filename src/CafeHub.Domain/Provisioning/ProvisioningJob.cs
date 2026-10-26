namespace CafeHub.Domain.Provisioning
{
    public sealed class ProvisioningJob
    {
        private ProvisioningJob()
        {
        }

        public Guid Id { get; private set; }

        public Guid TenantId { get; private set; }

        public ProvisioningJobStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public DateTimeOffset? DueAt { get; private set; }

        public DateTimeOffset? LeaseExpiresAt { get; private set; }

        public string? LeasedBy { get; private set; }

        public static ProvisioningJob Create(
            Guid id,
            Guid tenantId,
            ProvisioningJobStatus status,
            DateTimeOffset createdAt,
            DateTimeOffset? dueAt = null,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The provisioning job id is required.", nameof(id));
            }

            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(tenantId));
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("The provisioning job status is not defined.", nameof(status));
            }

            return new ProvisioningJob
            {
                Id = id,
                TenantId = tenantId,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                DueAt = dueAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public void Claim(string workerId, DateTimeOffset now, TimeSpan leaseDuration)
        {
            if (string.IsNullOrWhiteSpace(workerId))
            {
                throw new ArgumentException("The worker id is required.", nameof(workerId));
            }

            if (leaseDuration <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaseDuration),
                    "The lease duration must be positive.");
            }

            if (Status != ProvisioningJobStatus.Pending &&
                (Status != ProvisioningJobStatus.Running || LeaseExpiresAt is null || LeaseExpiresAt > now))
            {
                throw new InvalidOperationException("A provisioning job can only be claimed from Pending state, or from Running state when its lease has expired.");
            }

            if (DueAt is not null && DueAt > now)
            {
                throw new InvalidOperationException("The provisioning job is not due yet.");
            }

            Status = ProvisioningJobStatus.Running;
            LeasedBy = workerId;
            LeaseExpiresAt = now.Add(leaseDuration);
            UpdatedAt = now;
        }

        public void Start(DateTimeOffset now)
        {
            if (Status != ProvisioningJobStatus.Pending)
            {
                throw new InvalidOperationException("A provisioning job can only be started from Pending state.");
            }

            Status = ProvisioningJobStatus.Running;
            UpdatedAt = now;
        }

        public void Complete(DateTimeOffset now)
        {
            if (Status != ProvisioningJobStatus.Running)
            {
                throw new InvalidOperationException("A provisioning job can only be completed from Running state.");
            }

            Status = ProvisioningJobStatus.Succeeded;
            UpdatedAt = now;
        }

        public void Fail(DateTimeOffset now)
        {
            if (Status != ProvisioningJobStatus.Running)
            {
                throw new InvalidOperationException("A provisioning job can only fail from Running state.");
            }

            Status = ProvisioningJobStatus.Failed;
            UpdatedAt = now;
        }

        public void Retry(DateTimeOffset now)
        {
            if (Status != ProvisioningJobStatus.Failed)
            {
                throw new InvalidOperationException("A provisioning job can only be retried from Failed state.");
            }

            Status = ProvisioningJobStatus.Pending;
            UpdatedAt = now;
        }
    }
}
