using CafeHub.Application.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.WebApi.Authorization
{
    public sealed class TenantPermissionAuthorizationHandler(IServiceProvider services) : AuthorizationHandler<TenantPermissionRequirement>
    {
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantPermissionRequirement requirement)
        {
            if (context.Resource is not HttpContext httpContext || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId) ||
                !httpContext.Items.TryGetValue(typeof(TenantAccountAccess), out var access) || access is not TenantAccountAccess account)
            {
                return;
            }

            if (!account.IsStaff)
            {
                context.Succeed(requirement);
                return;
            }

            if (requirement.OwnerOnly || !account.HasActiveBranch || account.BranchId is not { } branchId)
            {
                return;
            }

            var permissions = services.GetRequiredService<ITenantPermissionReader>();
            if ((await permissions.ReadAsync(userId, httpContext.RequestAborted, branchId)).Contains(requirement.Code, StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
        }
    }
}
