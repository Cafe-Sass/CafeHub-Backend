using System.Diagnostics;
using System.Text.Json;
using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Abstractions.Results;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Authentication
{
    public class ProblemDetailsJwtBearerEvents : JwtBearerEvents
    {
        private const string UnauthorizedCode = "Identity.NotAuthenticated";
        private const string UnauthorizedMessage = "Authentication is required to use this resource.";
        private const string ForbiddenCode = "Identity.Forbidden";
        private const string ForbiddenMessage = "You are not allowed to use this resource.";
        private const string MissingTokenFailureCategory = "missing_token";

        private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly string ProblemJsonContentType = "application/problem+json";

        public override async Task Challenge(JwtBearerChallengeContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            context.HandleResponse();

            var failureCategory = context.Error ?? MissingTokenFailureCategory;
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ProblemDetailsJwtBearerEvents>>();
            logger?.LogWarning("Jwt bearer authentication rejected the request. Failure category: {FailureCategory}", failureCategory);

            await WriteProblemAsync(context.HttpContext, Error.Unauthorized(UnauthorizedCode, UnauthorizedMessage));
        }

        public override async Task Forbidden(ForbiddenContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            await WriteProblemAsync(context.HttpContext, Error.Forbidden(ForbiddenCode, ForbiddenMessage));
        }

        private static async Task WriteProblemAsync(HttpContext httpContext, Error error)
        {
            var correlationIdAccessor = httpContext.RequestServices.GetService<ICorrelationIdAccessor>();
            var traceIdentifier = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            var problemDetails = ErrorProblemDetailsMapper.Map(error, traceIdentifier, correlationIdAccessor?.CorrelationId);

            httpContext.Response.StatusCode = problemDetails.Status!.Value;
            httpContext.Response.ContentType = ProblemJsonContentType;
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails, ResponseJsonOptions);
        }
    }
}
