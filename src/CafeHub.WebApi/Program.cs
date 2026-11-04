using CafeHub.Application;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.WebApi;
using CafeHub.WebApi.Cors;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.HealthChecks;
using CafeHub.WebApi.Idempotency;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Security;
using CafeHub.WebApi.Transport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdempotencyKeyActionFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSuperAdminSeeding();
builder.Services.AddCorrelationId();
builder.Services.AddAuthRateLimiting(builder.Configuration);
builder.Services.AddTransportSecurity(builder.Configuration);
builder.Services.AddRequestLimits(builder.Configuration);
builder.Services.AddConfiguredCors(builder.Configuration);
builder.Services.AddGlobalRateLimiting(builder.Configuration);
builder.Services.AddPlatformAuthentication();
builder.Services.AddTenantAuthentication();
builder.Services.AddPlatformAuthorization();
builder.Services.AddTenantAuthorization();
builder.Services.AddApiDocumentation();

builder.Services.AddScoped<MasterDatabaseHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<MasterDatabaseHealthCheck>("master", tags: new[] { "ready" });

var app = builder.Build();
_ = app.Services.GetRequiredService<TenantDatabaseOptions>();

app.UseForwardedHeaders();
app.UseCorrelationId();
app.UseSecurityHeaders();
app.UseExceptionHandler();
app.UseTransportSecurity(app.Environment);
app.UseRateLimiter();
app.UseConfiguredCors();
app.UseMiddleware<CafeHub.WebApi.Tenancy.TenantResolutionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiDocumentation();
app.MapControllers().RequireRateLimiting(GlobalRateLimiting.PolicyName);

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync,
}).ExcludeFromDescription().WithMetadata(new DisableRateLimitingAttribute());

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
}).ExcludeFromDescription().WithMetadata(new DisableRateLimitingAttribute());

app.Run();
