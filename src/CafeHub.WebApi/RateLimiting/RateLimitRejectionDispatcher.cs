using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Dispatches rate-limit rejections to the correct writer based on the rejected policy's metadata. The
    /// <see cref="RateLimiterOptions.OnRejected"/> delegate is single, so this dispatcher preserves the
    /// existing auth rejection byte-for-byte while adding the global rejection.
    /// </summary>
    public static class RateLimitRejectionDispatcher
    {
        public static async ValueTask WriteAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var policyName = GetPolicyName(context.HttpContext);

            if (policyName == AuthRateLimiting.PolicyName)
            {
                await AuthRateLimitRejectionWriter.WriteAsync(context, cancellationToken);
            }
            else
            {
                await GlobalRateLimitRejectionWriter.WriteAsync(context, cancellationToken);
            }
        }

        private static string? GetPolicyName(HttpContext? httpContext)
        {
            if (httpContext is null)
            {
                return null;
            }

            var endpoint = httpContext.GetEndpoint();
            if (endpoint is null)
            {
                return null;
            }

            return endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
        }
    }
}
