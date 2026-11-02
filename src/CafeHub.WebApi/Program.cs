using CafeHub.Application;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Tenancy;
using CafeHub.WebApi;
using CafeHub.WebApi.Cors;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Idempotency;
using CafeHub.WebApi.RateLimiting;
using CafeHub.WebApi.Security;
using CafeHub.WebApi.Transport;

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

app.Run();
