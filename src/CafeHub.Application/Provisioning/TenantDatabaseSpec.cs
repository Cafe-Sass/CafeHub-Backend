namespace CafeHub.Application.Provisioning
{
    /// <summary>
    /// Identifies the tenant environment a provisioning operation targets. Carries the tenant id and the
    /// slug the database and role names are derived from, per the reviewed PostgreSQL operations commands.
    /// </summary>
    public sealed record TenantDatabaseSpec(Guid TenantId, string TenantSlug);
}
