using CafeHub.Application.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Authentication
{
    public sealed class TenantJwtBearerEvents : ProblemDetailsJwtBearerEvents
    {
        private const string TenantKindCategory = "token_kind";
        private const string TenantMismatchCategory = "tenant_mismatch";
        private const string NoTenantContextCategory = "no_tenant_context";
        private static readonly object TenantUnavailableItemKey = new();

        public override Task TokenValidated(TokenValidatedContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            var logger = context.HttpContext.RequestServices.GetService<ILogger<TenantJwtBearerEvents>>();
            var principal = context.Principal;
            if (principal?.FindFirst("token_kind")?.Value != "tenant")
            {
                Reject(context, logger, TenantKindCategory);
                return Task.CompletedTask;
            }

            var tenantClaim = principal.FindFirst("tid")?.Value;
            if (!Guid.TryParse(tenantClaim, out var tokenTenantId))
            {
                Reject(context, logger, TenantMismatchCategory);
                return Task.CompletedTask;
            }

            try
            {
                var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>().GetRequired();
                if (!tenantContext.IsActive)
                {
                    Reject(context, logger, NoTenantContextCategory);
                    return Task.CompletedTask;
                }

                if (tokenTenantId != tenantContext.TenantId)
                {
                    context.HttpContext.Items[TenantUnavailableItemKey] = true;
                    Reject(context, logger, TenantMismatchCategory);
                }
            }
            catch (TenantIsolationException)
            {
                Reject(context, logger, NoTenantContextCategory);
            }

            return Task.CompletedTask;
        }

        public override async Task Challenge(JwtBearerChallengeContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.HttpContext.Items.ContainsKey(TenantUnavailableItemKey))
            {
                await base.Challenge(context);
                return;
            }

            context.HttpContext.Items.Remove(TenantUnavailableItemKey);
            context.HandleResponse();

            var logger = context.HttpContext.RequestServices.GetService<ILogger<TenantJwtBearerEvents>>();
            logger?.LogWarning("Tenant bearer authentication rejected the request. Failure category: {FailureCategory}", TenantMismatchCategory);

            await WriteProblemAsync(context.HttpContext, TenancyErrors.TenantUnavailable());
        }

        private static void Reject(TokenValidatedContext context, ILogger<TenantJwtBearerEvents>? logger, string category)
        {
            logger?.LogWarning("Tenant bearer authentication rejected the request. Failure category: {FailureCategory}", category);
            context.Fail("Tenant authentication failed.");
        }
    }
}
