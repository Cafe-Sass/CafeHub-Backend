using CafeHub.Application.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Authentication
{
    public sealed class TenantJwtBearerEvents : ProblemDetailsJwtBearerEvents
    {
        public override Task TokenValidated(TokenValidatedContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var logger = context.HttpContext.RequestServices.GetService<ILogger<TenantJwtBearerEvents>>();
            var principal = context.Principal;
            if (principal?.FindFirst("token_kind")?.Value != "tenant")
            {
                Reject(context, logger, "token_kind");
                return Task.CompletedTask;
            }

            var tenantClaim = principal.FindFirst("tid")?.Value;
            if (!Guid.TryParse(tenantClaim, out var tokenTenantId))
            {
                Reject(context, logger, "tenant_mismatch");
                return Task.CompletedTask;
            }

            try
            {
                var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>().GetRequired();
                if (!tenantContext.IsActive)
                {
                    Reject(context, logger, "no_tenant_context");
                    return Task.CompletedTask;
                }

                if (tokenTenantId != tenantContext.TenantId)
                {
                    Reject(context, logger, "tenant_mismatch");
                }
            }
            catch (TenantIsolationException)
            {
                Reject(context, logger, "no_tenant_context");
            }

            return Task.CompletedTask;
        }

        private static void Reject(TokenValidatedContext context, ILogger<TenantJwtBearerEvents>? logger, string category)
        {
            logger?.LogWarning("Tenant bearer authentication rejected the request. Failure category: {FailureCategory}", category);
            context.Fail("Tenant authentication failed.");
        }
    }
}
