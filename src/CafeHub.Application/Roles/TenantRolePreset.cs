namespace CafeHub.Application.Roles
{
    public sealed record TenantRolePreset(string Name, IReadOnlyList<string> PermissionCodes);
}
