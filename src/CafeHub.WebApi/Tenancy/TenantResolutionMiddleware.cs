using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Tenancy;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Tenancy
{
    public sealed class TenantResolutionMiddleware
    {
        private const string ProblemJson = "application/problem+json";
        private const string MalformedSlugCategory = "malformed_slug";
        private const string UnavailableCategory = "unavailable";
        private readonly RequestDelegate next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
        }

        public async Task InvokeAsync(
            HttpContext context,
            ITenantResolver tenantResolver,
            ITenantStateGuard tenantStateGuard,
            ITenantContextSetter tenantContextSetter,
            ILogger<TenantResolutionMiddleware>? logger = null,
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            if (!IsTenantPath(context.Request.Path))
            {
                await next(context);
                return;
            }

            if (!TenantRouteSlugParser.TryGetSlug(context.Request.Path, out var slug))
            {
                LogRejected(logger, MalformedSlugCategory);
                await WriteUnavailableAsync(context, correlationIdAccessor);
                return;
            }

            var resolved = await tenantResolver.ResolveAsync(slug, context.RequestAborted);
            if (resolved.IsFailure)
            {
                LogRejected(logger, UnavailableCategory);
                await WriteUnavailableAsync(context, correlationIdAccessor);
                return;
            }

            if (tenantStateGuard.EnsureUsable(resolved.Value).IsFailure)
            {
                LogRejected(logger, UnavailableCategory);
                await WriteUnavailableAsync(context, correlationIdAccessor);
                return;
            }

            var tenant = resolved.Value;
            tenantContextSetter.Set(TenantContext.Create(tenant.TenantId, tenant.SecretReference!, true, tenant.Slug));

            using var scope = logger?.BeginScope(new Dictionary<string, object> { ["TenantId"] = tenant.TenantId });
            await next(context);
        }

        private static void LogRejected(ILogger<TenantResolutionMiddleware>? logger, string category)
        {
            logger?.LogWarning("Tenant resolution rejected the request. Failure category: {FailureCategory}", category);
        }

        private static bool IsTenantPath(PathString path)
        {
            if (!path.HasValue)
            {
                return false;
            }

            var value = path.Value!;
            return value.Equals("/t", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("/t/", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task WriteUnavailableAsync(HttpContext context, ICorrelationIdAccessor? correlationIdAccessor)
        {
            var problem = ErrorProblemDetailsMapper.Map(
                TenancyErrors.TenantUnavailable(),
                context.TraceIdentifier,
                correlationIdAccessor?.CorrelationId);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = ProblemJson;
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemJson, cancellationToken: context.RequestAborted);
        }
    }
}
