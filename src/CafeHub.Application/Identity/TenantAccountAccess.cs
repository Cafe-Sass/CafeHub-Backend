namespace CafeHub.Application.Identity
{
    public sealed record TenantAccountAccess(bool IsStaff, string? SessionVersion, bool HasActiveBranch);
}
