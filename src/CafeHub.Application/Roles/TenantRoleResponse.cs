namespace CafeHub.Application.Roles
{
    public sealed record TenantRoleResponse(Guid Id, string Name, IReadOnlyList<string> PermissionCodes, string ConcurrencyToken);
}
