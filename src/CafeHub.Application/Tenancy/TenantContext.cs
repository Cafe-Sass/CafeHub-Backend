namespace CafeHub.Application.Tenancy
{
    public sealed record TenantContext
    {
        private TenantContext(Guid tenantId, string secretReference, bool isActive)
        {
            TenantId = tenantId;
            SecretReference = secretReference;
            IsActive = isActive;
        }

        public Guid TenantId { get; }

        public string SecretReference { get; }

        public bool IsActive { get; }

        public static TenantContext Create(Guid tenantId, string secretReference, bool isActive)
        {
            if (tenantId == Guid.Empty)
            {
                throw new InvalidTenantContextException("A tenant context cannot be created with an empty tenant id.");
            }

            if (string.IsNullOrWhiteSpace(secretReference))
            {
                throw new InvalidTenantContextException("A tenant context cannot be created with a blank secret reference.");
            }

            return new TenantContext(tenantId, secretReference, isActive);
        }
    }
}
