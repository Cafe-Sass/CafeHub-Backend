using CafeHub.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;

namespace CafeHub.WebApi.Authorization
{
    public sealed class TenantBoundAuthorizationHandler : AuthorizationHandler<TenantBoundRequirement>
    {
        private readonly ITenantContextAccessor tenantContextAccessor;

        public TenantBoundAuthorizationHandler(ITenantContextAccessor tenantContextAccessor)
        {
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantBoundRequirement requirement)
        {
            var tenantIdValue = context.User.FindFirst("tid")?.Value;
            if (context.User.Identity?.IsAuthenticated == true
                && Guid.TryParse(tenantIdValue, out var tokenTenantId))
            {
                try
                {
                    var tenantContext = tenantContextAccessor.GetRequired();
                    if (tenantContext.IsActive && tenantContext.TenantId == tokenTenantId)
                    {
                        context.Succeed(requirement);
                    }
                }
                catch (TenantIsolationException)
                {
                }
            }

            return Task.CompletedTask;
        }
    }
}
