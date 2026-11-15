using Microsoft.AspNetCore.Authorization;

namespace CafeHub.WebApi.Authorization
{
    public sealed record TenantPermissionRequirement(string Code, bool OwnerOnly) : IAuthorizationRequirement;
}
