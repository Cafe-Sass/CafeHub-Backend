namespace CafeHub.Application.Tenancy
{
    public sealed record TenantContext
    {
        private TenantContext(Guid tenantId, string secretReference, bool isActive, string? slug)
        {
            TenantId = tenantId;
            SecretReference = secretReference;
            IsActive = isActive;
            Slug = slug;
        }

        public Guid TenantId { get; }

        public string SecretReference { get; }

        public bool IsActive { get; }

        public string? Slug { get; }

        public static TenantContext Create(Guid tenantId, string secretReference, bool isActive, string? slug = null)
        {
            if (tenantId == Guid.Empty)
            {
                throw new InvalidTenantContextException("A tenant context cannot be created with an empty tenant id.");
            }

            if (string.IsNullOrWhiteSpace(secretReference))
            {
                throw new InvalidTenantContextException("A tenant context cannot be created with a blank secret reference.");
            }

            if (slug is not null && string.IsNullOrWhiteSpace(slug))
            {
                throw new InvalidTenantContextException("A tenant context slug cannot be blank when supplied.");
            }

            return new TenantContext(tenantId, secretReference, isActive, slug);
        }
    }
}
