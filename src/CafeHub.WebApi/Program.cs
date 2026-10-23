using CafeHub.Application;
using CafeHub.Infrastructure;
using CafeHub.WebApi;
using CafeHub.WebApi.Errors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCorrelationId();

var app = builder.Build();

app.UseCorrelationId();
app.UseExceptionHandler();

app.MapControllers();

app.Run();
