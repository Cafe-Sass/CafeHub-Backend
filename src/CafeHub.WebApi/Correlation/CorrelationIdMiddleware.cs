using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CafeHub.WebApi.Correlation
{
    public sealed class CorrelationIdMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger<CorrelationIdMiddleware> logger;
        private readonly ICorrelationIdGenerator generator;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger, ICorrelationIdGenerator generator)
        {
            this.next = next ?? throw new ArgumentNullException(nameof(next));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            httpContext.Request.Headers.TryGetValue(CorrelationIdConstants.HeaderName, out var headerValues);
            var incoming = headerValues.ToString();
            var isReused = CorrelationIdPolicy.IsValid(incoming);
            var correlationId = isReused ? incoming : generator.Generate();

            httpContext.Items[CorrelationIdConstants.ItemKey] = correlationId;
            httpContext.Response.OnStarting(() =>
            {
                httpContext.Response.Headers[CorrelationIdConstants.HeaderName] = correlationId;
                return Task.CompletedTask;
            });

            using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
            {
                if (incoming.Length > 0 && !isReused)
                {
                    logger.LogWarning("The X-Correlation-Id header was rejected and replaced with a generated correlation identifier. Rejected header length: {RejectedCorrelationIdLength}", incoming.Length);
                }

                await next(httpContext);
            }
        }
    }
}
