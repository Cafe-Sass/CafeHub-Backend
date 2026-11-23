namespace CafeHub.Application.Media
{
    public readonly record struct MediaScope
    {
        private MediaScope(Guid? tenantId)
        {
            TenantId = tenantId;
        }

        public static MediaScope Platform => new(null);

        public Guid? TenantId { get; }

        public bool IsPlatform => TenantId is null;

        public static MediaScope ForTenant(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("A media tenant scope requires a tenant id.", nameof(tenantId));
            }

            return new MediaScope(tenantId);
        }
    }
}
