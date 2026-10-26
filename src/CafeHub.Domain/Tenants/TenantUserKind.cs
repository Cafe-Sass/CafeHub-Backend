namespace CafeHub.Domain.Tenants
{
    /// <summary>
    /// The kind of tenant user account: the seeded cafe owner versus staff accounts.
    /// Role claims remain the authoritative authorization input.
    /// </summary>
    public enum TenantUserKind
    {
        Owner,
        Staff,
    }
}
