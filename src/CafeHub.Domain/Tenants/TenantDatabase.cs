using CafeHub.Domain.Tenants.Credentials;

namespace CafeHub.Domain.Tenants
{
    public sealed class TenantDatabase
    {
        private TenantDatabase()
        {
        }

        public Guid Id { get; private set; }

        public Guid TenantId { get; private set; }

        public string SecretReference { get; private set; } = string.Empty;

        public TenantDatabaseStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static TenantDatabase Create(
            Guid id,
            Guid tenantId,
            string secretReference,
            TenantDatabaseStatus status,
            DateTimeOffset createdAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant database id is required.", nameof(id));
            }

            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(tenantId));
            }

            if (string.IsNullOrWhiteSpace(secretReference) || secretReference.Length > 128)
            {
                throw new ArgumentException(
                    "The secret reference must be between 1 and 128 characters.",
                    nameof(secretReference));
            }

            if (!TenantDatabaseCredentialReference.IsValidForTenant(secretReference, tenantId))
            {
                throw new ArgumentException(
                    "The secret reference must start with a lowercase letter or digit and contain only lowercase letters, digits, and hyphens.",
                    nameof(secretReference));
            }

            if (!Enum.IsDefined(status))
            {
                throw new ArgumentException("The tenant database status is not defined.", nameof(status));
            }

            return new TenantDatabase
            {
                Id = id,
                TenantId = tenantId,
                SecretReference = secretReference,
                Status = status,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public void MarkProvisioned(string secretReference, DateTimeOffset now)
        {
            if (string.IsNullOrWhiteSpace(secretReference) || secretReference.Length > 128)
            {
                throw new ArgumentException(
                    "The secret reference must be between 1 and 128 characters.",
                    nameof(secretReference));
            }

            if (!TenantDatabaseCredentialReference.IsValidForTenant(secretReference, TenantId))
            {
                throw new ArgumentException(
                    "The secret reference must start with a lowercase letter or digit and contain only lowercase letters, digits, and hyphens.",
                    nameof(secretReference));
            }

            SecretReference = secretReference;
            Status = TenantDatabaseStatus.Provisioned;
            UpdatedAt = now;
        }
    }
}
