namespace CafeHub.Application.Tenancy
{
    public interface ITenantRegistryReader
    {
        Task<TenantRegistration?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken);

        Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken);
    }
}
