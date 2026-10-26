namespace CafeHub.Application.Provisioning
{
    /// <summary>
    /// The input a tenant schema initializer needs to apply the Tenant migration and seed the
    /// Owner account, cafe profile, and reference data. It carries the password hash only,
    /// never a raw password.
    /// </summary>
    public sealed record TenantInitializationRequest(
        Guid TenantId,
        string TenantSlug,
        string OwnerEmail,
        string OwnerFullName,
        string? OwnerPhoneNumber,
        string OwnerPasswordHash,
        string CafeName,
        string PlanReference);

}
