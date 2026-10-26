namespace CafeHub.Domain.Tenants
{
    public sealed class TenantRoleClaim
    {
        private TenantRoleClaim()
        {
        }

        public Guid Id { get; private set; }

        public Guid RoleId { get; private set; }

        public string ClaimType { get; private set; } = string.Empty;

        public string? ClaimValue { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static TenantRoleClaim Create(
            Guid id,
            Guid roleId,
            string claimType,
            string? claimValue,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant role claim id is required.", nameof(id));
            }

            if (roleId == Guid.Empty)
            {
                throw new ArgumentException("The role id is required.", nameof(roleId));
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

            return new TenantRoleClaim
            {
                Id = id,
                RoleId = roleId,
                ClaimType = claimType,
                ClaimValue = claimValue,
                CreatedAt = createdAt,
            };
        }
    }
}
