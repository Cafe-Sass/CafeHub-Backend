namespace CafeHub.WebApi.Controllers
{
    public sealed record UpdateTenantRoleRequest(string? Name, IReadOnlyList<string>? PermissionCodes, string? ConcurrencyToken);
}
