using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tenancy
{
    public sealed record ResolvedTenant(
        Guid TenantId,
        string Slug,
        TenantLifecycleStatus State,
        string? SecretReference);
}
