using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tenancy
{
    /// <summary>
    /// The registry projection for tenant resolution: the exact ERD columns of a tenants row joined with its
    /// zero-or-one tenant_databases row. Holds no personal data and no subscription data. The database and
    /// role names are not registry columns; they derive from the slug, exactly as provisioning already does.
    /// </summary>
    public sealed record TenantRegistration(
        Guid TenantId,
        string Slug,
        TenantLifecycleStatus State,
        string? SecretReference);
}
