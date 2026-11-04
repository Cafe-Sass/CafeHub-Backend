using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CafeHub.WebApi.HealthChecks
{
    public static class HealthResponseWriter
    {
        public static Task WriteAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            var status = report.Status == HealthStatus.Healthy ? "Healthy" : "Unhealthy";
            return context.Response.WriteAsync($"{{\"status\":\"{status}\"}}", context.RequestAborted);
        }
    }
}
