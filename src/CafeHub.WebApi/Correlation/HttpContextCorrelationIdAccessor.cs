using CafeHub.Application.Abstractions.Correlation;
using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.Correlation
{
    public sealed class HttpContextCorrelationIdAccessor : ICorrelationIdAccessor
    {
        private readonly IHttpContextAccessor httpContextAccessor;

        public HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public string? CorrelationId
        {
            get
            {
                var httpContext = this.httpContextAccessor.HttpContext;
                if (httpContext is null)
                {
                    return null;
                }

                return httpContext.Items.TryGetValue(CorrelationIdConstants.ItemKey, out var value) && value is string correlationId
                    ? correlationId
                    : null;
            }
        }
    }
}
