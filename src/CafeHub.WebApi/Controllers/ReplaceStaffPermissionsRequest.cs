namespace CafeHub.WebApi.Controllers
{
    public sealed record ReplaceStaffPermissionsRequest(IReadOnlyList<string>? PermissionCodes);
}
