namespace CafeHub.WebApi.Controllers
{
    public sealed record ReplaceStaffRolesRequest(IReadOnlyList<Guid>? RoleIds);
}
