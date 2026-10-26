namespace CafeHub.Domain.Tenants
{
    public sealed class TenantUserClaim
    {
        private TenantUserClaim()
        {
        }

        public Guid Id { get; private set; }

        public Guid TenantUserId { get; private set; }

        public string ClaimType { get; private set; } = string.Empty;

        public string? ClaimValue { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static TenantUserClaim Create(
            Guid id,
            Guid tenantUserId,
            string claimType,
            string? claimValue,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant user claim id is required.", nameof(id));
            }

            if (tenantUserId == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(tenantUserId));
            }

            if (string.IsNullOrWhiteSpace(claimType) || claimType.Length > 512)
            {
                throw new ArgumentException(
                    "The claim type must be between 1 and 512 characters.",
                    nameof(claimType));
            }

            if (claimValue is not null && claimValue.Length > 1024)
            {
                throw new ArgumentException(
                    "The claim value must not exceed 1024 characters.",
                    nameof(claimValue));
            }

            return new TenantUserClaim
            {
                Id = id,
                TenantUserId = tenantUserId,
                ClaimType = claimType,
                ClaimValue = claimValue,
                CreatedAt = createdAt,
            };
        }
    }
}
