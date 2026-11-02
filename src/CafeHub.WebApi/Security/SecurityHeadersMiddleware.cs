using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;

namespace CafeHub.WebApi.Security
{
    /// <summary>
    /// Writes the agreed security response headers on every response and applies the configured maximum
    /// request body size per request. CSP and Cache-Control are applied only to API and tenant paths; in
    /// Development the documentation routes are exempt so Swagger UI still renders. Headers are written
    /// before the downstream handler runs so they also appear on error responses produced by the exception
    /// handler.
    /// </summary>
    public sealed class SecurityHeadersMiddleware
    {
        private const string DocumentationRootPath = "/";
        private const string OpenApiDocumentPath = "/openapi/v1.json";
        private const string SwaggerUiPath = "/swagger";

        private const string ApiPathPrefix = "/api/";
        private const string TenantPathPrefix = "/t/";

        private readonly RequestDelegate next;
        private readonly IHostEnvironment environment;
        private readonly RequestLimitsOptions options;

        public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment, RequestLimitsOptions options)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var bodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (bodySizeFeature is not null && !bodySizeFeature.IsReadOnly)
            {
                bodySizeFeature.MaxRequestBodySize = options.MaxBodyBytes;
            }

            WriteHeaders(context.Response, context.Request.Path.Value ?? string.Empty);

            await next(context);
        }

        private static void WriteHeaders(HttpResponse response, string path)
        {
            response.Headers[SecurityHeaderValues.XContentTypeOptionsName] = SecurityHeaderValues.XContentTypeOptionsValue;
            response.Headers[SecurityHeaderValues.XFrameOptionsName] = SecurityHeaderValues.XFrameOptionsValue;
            response.Headers[SecurityHeaderValues.ReferrerPolicyName] = SecurityHeaderValues.ReferrerPolicyValue;
            response.Headers[SecurityHeaderValues.PermissionsPolicyName] = SecurityHeaderValues.PermissionsPolicyValue;

            if (IsDocumentationPath(path))
            {
                // Documentation routes are exempt from CSP and cache headers in Development.
                // Outside Development the documentation routes are not mapped, so no exemption applies.
            }
            else if (IsApiOrTenantPath(path))
            {
                response.Headers[SecurityHeaderValues.ContentSecurityPolicyName] = SecurityHeaderValues.ContentSecurityPolicyValue;
                response.Headers[SecurityHeaderValues.CacheControlName] = SecurityHeaderValues.CacheControlValue;
            }
        }

        private static bool IsDocumentationPath(string path)
        {
            return path == DocumentationRootPath
                || path == OpenApiDocumentPath
                || path == SwaggerUiPath
                || path.StartsWith(SwaggerUiPath + "/", StringComparison.Ordinal);
        }

        private static bool IsApiOrTenantPath(string path)
        {
            return path.StartsWith(ApiPathPrefix, StringComparison.Ordinal)
                || path.StartsWith(TenantPathPrefix, StringComparison.Ordinal);
        }
    }
}
