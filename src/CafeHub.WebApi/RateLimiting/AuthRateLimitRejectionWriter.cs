using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using CafeHub.Application.Abstractions.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Writes the single 429 response for a rejected admin authentication request. The body follows the
    /// same RFC 9457 shape as every other API error: a stable <c>code</c>, a safe <c>detail</c>, the
    /// <c>traceId</c>, and the request's <c>correlationId</c> when one exists. The <c>Retry-After</c> header
    /// carries the whole seconds until the current fixed window resets. No request header, body, token, or
    /// credential is ever echoed.
    /// </summary>
    public static class AuthRateLimitRejectionWriter
    {
        private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

        private static readonly string ProblemJsonContentType = "application/problem+json";

        public static async ValueTask WriteAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.HttpContext is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.Lease is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var httpContext = context.HttpContext;
            var problemDetails = CreateProblemDetails(httpContext);
            AddRetryAfter(httpContext.Response, context.Lease);

            httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            httpContext.Response.ContentType = ProblemJsonContentType;
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, problemDetails, ResponseJsonOptions, cancellationToken);
        }

        private static ProblemDetails CreateProblemDetails(HttpContext httpContext)
        {
            var traceIdentifier = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            var problemDetails = new ProblemDetails
            {
                Type = AuthRateLimiting.ProblemType,
                Title = AuthRateLimiting.ProblemTitle,
                Status = StatusCodes.Status429TooManyRequests,
                Detail = AuthRateLimiting.ErrorDetail,
            };
            problemDetails.Extensions["code"] = AuthRateLimiting.ErrorCode;
            problemDetails.Extensions["traceId"] = traceIdentifier;

            var correlationId = httpContext.RequestServices.GetService<ICorrelationIdAccessor>()?.CorrelationId;
            if (!string.IsNullOrWhiteSpace(correlationId))
            {
                problemDetails.Extensions["correlationId"] = correlationId;
            }

            return problemDetails;
        }

        private static void AddRetryAfter(HttpResponse response, RateLimitLease lease)
        {
            if (!lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                return;
            }

            var retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            if (retryAfterSeconds < 1)
            {
                retryAfterSeconds = 1;
            }

            response.Headers[HeaderNames.RetryAfter] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        }
    }
}
