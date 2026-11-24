using CafeHub.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.WebApi.Authorization
{
    public sealed class TenantAnyPermissionAuthorizationHandler(IServiceProvider services)
        : AuthorizationHandler<TenantAnyPermissionRequirement>
    {
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            TenantAnyPermissionRequirement requirement)
        {
            if (context.Resource is not HttpContext httpContext
                || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId)
                || !httpContext.Items.TryGetValue(typeof(TenantAccountAccess), out var access)
                || access is not TenantAccountAccess account)
            {
                return;
            }

            if (!account.IsStaff)
            {
                context.Succeed(requirement);
                return;
            }

            if (!account.HasActiveBranch || account.BranchId is not { } branchId)
            {
                return;
            }

            var permissions = services.GetRequiredService<ITenantPermissionReader>();
            var granted = await permissions.ReadAsync(userId, httpContext.RequestAborted, branchId);
            if (requirement.Codes.Any(code => granted.Contains(code, StringComparer.Ordinal)))
            {
                context.Succeed(requirement);
            }
        }
    }
}
