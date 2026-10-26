using CafeHub.Application.Abstractions.Correlation;
using CafeHub.Application.Tenancy;
using CafeHub.WebApi.Errors;
using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.Tenancy
{
    public sealed class TenantResolutionMiddleware
    {
        private const string ProblemJson = "application/problem+json";
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
            ICorrelationIdAccessor? correlationIdAccessor = null)
        {
            if (!IsTenantPath(context.Request.Path))
            {
                await next(context);
                return;
            }

            if (!TenantRouteSlugParser.TryGetSlug(context.Request.Path, out var slug))
            {
                await WriteUnavailableAsync(context, correlationIdAccessor);
                return;
            }

            var resolved = await tenantResolver.ResolveAsync(slug, context.RequestAborted);
            if (resolved.IsFailure)
            {
                await WriteUnavailableAsync(context, correlationIdAccessor);
                return;
            }

            if (tenantStateGuard.EnsureUsable(resolved.Value).IsFailure)
            {
                await WriteUnavailableAsync(context, correlationIdAccessor);
                return;
            }

            var tenant = resolved.Value;
            tenantContextSetter.Set(TenantContext.Create(tenant.TenantId, tenant.SecretReference!, true));
            await next(context);
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
