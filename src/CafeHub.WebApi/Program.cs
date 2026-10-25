using CafeHub.Application;
using CafeHub.Infrastructure;
using CafeHub.WebApi;
using CafeHub.WebApi.Errors;
using CafeHub.WebApi.Idempotency;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<IdempotencyKeyActionFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCorrelationId();
builder.Services.AddAuthRateLimiting(builder.Configuration);
builder.Services.AddPlatformAuthentication();
builder.Services.AddPlatformAuthorization();
builder.Services.AddApiDocumentation();

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapApiDocumentation();
app.MapControllers();

app.Run();
