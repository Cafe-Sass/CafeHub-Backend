using System.Diagnostics;
using System.Text.Json;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tenancy;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Errors
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private const string UnexpectedCode = "Server.Unexpected";
        private const string UnexpectedMessage = "An unexpected error occurred. Try again later.";
        private const string TenantContextInvalidCode = "Tenant.ContextInvalid";
        private const string TenantContextInvalidMessage = "A valid tenant is required to use this service.";
        private const string TenantUnavailableCode = "Tenant.Unavailable";
        private const string TenantUnavailableMessage = "The requested tenant is not available.";

        private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly string ProblemJsonContentType = "application/problem+json";

        private readonly ILogger<GlobalExceptionHandler> logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (exception is null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            var traceIdentifier = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            var problemDetails = CreateProblemDetails(exception, traceIdentifier);

            logger.LogError(exception, "Request {TraceId} failed with {ExceptionType}", traceIdentifier, exception.GetType().Name);

            httpContext.Response.StatusCode = problemDetails.Status!.Value;
            httpContext.Response.ContentType = ProblemJsonContentType;
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails, ResponseJsonOptions, cancellationToken);

            return true;
        }

        private static ProblemDetails CreateProblemDetails(Exception exception, string traceIdentifier)
        {
            return exception switch
            {
                TenantInactiveException or TenantConnectionNotConfiguredException =>
                    ErrorProblemDetailsMapper.Map(Error.NotFound(TenantUnavailableCode, TenantUnavailableMessage), traceIdentifier),
                TenantIsolationException =>
                    ErrorProblemDetailsMapper.Map(Error.Validation(TenantContextInvalidCode, TenantContextInvalidMessage), traceIdentifier),
                _ =>
                    ErrorProblemDetailsMapper.Map(Error.Failure(UnexpectedCode, UnexpectedMessage), traceIdentifier),
            };
        }
    }
}
