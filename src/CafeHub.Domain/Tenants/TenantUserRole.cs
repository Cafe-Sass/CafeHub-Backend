namespace CafeHub.Domain.Tenants
{
    public sealed class TenantUserRole
    {
        private TenantUserRole()
        {
        }

        public Guid Id { get; private set; }

        public Guid TenantUserId { get; private set; }

        public Guid RoleId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static TenantUserRole Create(
            Guid id,
            Guid tenantUserId,
            Guid roleId,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The tenant user role id is required.", nameof(id));
            }

            if (tenantUserId == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(tenantUserId));
            }

            if (roleId == Guid.Empty)
            {
                throw new ArgumentException("The role id is required.", nameof(roleId));
            }

            return new TenantUserRole
            {
                Id = id,
                TenantUserId = tenantUserId,
                RoleId = roleId,
                CreatedAt = createdAt,
            };
        }
    }
}
