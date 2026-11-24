using Microsoft.AspNetCore.Authorization;

namespace CafeHub.WebApi.Authorization
{
    public sealed record TenantAnyPermissionRequirement(IReadOnlyList<string> Codes) : IAuthorizationRequirement;
}
