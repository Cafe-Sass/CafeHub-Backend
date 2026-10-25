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

        public static ProvisioningJob Create(
            Guid id,
            Guid tenantId,
            ProvisioningJobStatus status,
            DateTimeOffset createdAt,
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
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
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
