namespace CafeHub.Domain.Tenants
{
    public sealed class TenantSchemaUpgradeRun
    {
        private const int MaxMigrationIdLength = 150;

        private TenantSchemaUpgradeRun()
        {
        }

        public Guid Id { get; private set; }

        public Guid TenantId { get; private set; }

        public string TargetMigrationId { get; private set; } = string.Empty;

        public TenantSchemaUpgradeRunStatus Status { get; private set; }

        public string? FailureCode { get; private set; }

        public int AttemptCount { get; private set; }

        public Guid? RequestedBy { get; private set; }

        public DateTimeOffset? StartedAt { get; private set; }

        public DateTimeOffset? FinishedAt { get; private set; }

        public DateTimeOffset? LeaseExpiresAt { get; private set; }

        public string? LeasedBy { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static TenantSchemaUpgradeRun Create(
            Guid id,
            Guid tenantId,
            string targetMigrationId,
            Guid? requestedBy,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant schema upgrade run id is required.", nameof(id));
            }

            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(tenantId));
            }

            if (string.IsNullOrWhiteSpace(targetMigrationId) || targetMigrationId.Length > MaxMigrationIdLength)
            {
                throw new ArgumentException(
                    "The target migration id must be between 1 and 150 characters.",
                    nameof(targetMigrationId));
            }

            return new TenantSchemaUpgradeRun
            {
                Id = id,
                TenantId = tenantId,
                TargetMigrationId = targetMigrationId,
                Status = TenantSchemaUpgradeRunStatus.Pending,
                AttemptCount = 0,
                RequestedBy = requestedBy,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
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

            if (Status != TenantSchemaUpgradeRunStatus.Pending &&
                (Status != TenantSchemaUpgradeRunStatus.Running ||
                 LeaseExpiresAt is null ||
                 LeaseExpiresAt > now))
            {
                throw new InvalidOperationException(
                    "A tenant schema upgrade run can only be claimed from Pending state, or from Running state when its lease has expired.");
            }

            Status = TenantSchemaUpgradeRunStatus.Running;
            AttemptCount++;
            StartedAt ??= now;
            LeasedBy = workerId;
            LeaseExpiresAt = now.Add(leaseDuration);
            UpdatedAt = now;
        }

        public void Succeed(DateTimeOffset now)
        {
            if (Status != TenantSchemaUpgradeRunStatus.Running)
            {
                throw new InvalidOperationException(
                    "A tenant schema upgrade run can only succeed from Running state.");
            }

            Status = TenantSchemaUpgradeRunStatus.Succeeded;
            FinishedAt = now;
            LeasedBy = null;
            LeaseExpiresAt = null;
            UpdatedAt = now;
        }

        public void Fail(DateTimeOffset now, string failureCode)
        {
            if (Status != TenantSchemaUpgradeRunStatus.Running)
            {
                throw new InvalidOperationException(
                    "A tenant schema upgrade run can only fail from Running state.");
            }

            if (!TenantSchemaUpgradeFailureCodes.IsDefined(failureCode))
            {
                throw new ArgumentException(
                    "The tenant schema upgrade failure code is not defined.",
                    nameof(failureCode));
            }

            Status = TenantSchemaUpgradeRunStatus.Failed;
            FailureCode = failureCode;
            FinishedAt = now;
            LeasedBy = null;
            LeaseExpiresAt = null;
            UpdatedAt = now;
        }
    }
}
