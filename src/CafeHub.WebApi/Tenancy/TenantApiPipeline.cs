using CafeHub.WebApi.Cors;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Security;
using CafeHub.WebApi.Transport;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.WebApi.Tenancy
{
    internal static class TenantApiPipeline
    {
        internal static void ConfigureTenantApi(this WebApplication app)
        {
            app.UseRouting();
            app.UseMiddleware<TableQrResponseMiddleware>();
            app.UseExceptionHandler();
            app.UseTransportSecurity(app.Environment);
            app.UseConfiguredCors();
            app.UseRateLimiter();
            app.UseMiddleware<TenantResolutionMiddleware>();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers().RequireRateLimiting(GlobalRateLimiting.PolicyName);
        }
    }
}
