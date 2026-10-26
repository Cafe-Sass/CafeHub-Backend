namespace CafeHub.Application.Provisioning
{
    /// <summary>
    /// The data a tenant seeder writes in one idempotent transaction. Nothing here is a secret:
    /// the owner password hash is copied from the approved onboarding request and never re-hashed.
    /// </summary>
    public sealed record TenantSeedData(
        Guid OwnerId,
        Guid RoleId,
        Guid UserRoleId,
        Guid CafeSettingsId,
        Guid AuditRecordId,
        string OwnerEmail,
        string OwnerFullName,
        string? OwnerPhoneNumber,
        string OwnerPasswordHash,
        string CafeName,
        string Timezone,
        string SecurityStamp,
        string ConcurrencyStamp,
        string UserName,
        string NormalizedUserName,
        string DatabaseName);

}
