namespace CafeHub.Domain.Identity
{
    /// <summary>
    /// Tenant role names. The MVP seeds the Owner role; further roles are configured by
    /// later staff stories. Values are not invented beyond what the ERD seeding section
    /// documents.
    /// </summary>
    public static class TenantUserRoles
    {
        public const string Owner = "Owner";
    }
}
