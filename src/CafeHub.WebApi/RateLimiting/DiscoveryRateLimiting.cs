using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// The stricter policy for the anonymous guest discovery endpoint. It keeps the global window and
    /// partition key and admits half of the global permit limit, never less than one request, so a guest
    /// cannot spend provider capacity faster than the rest of the public surface.
    /// </summary>
    public static class DiscoveryRateLimiting
    {
        public const string PolicyName = "GuestDiscovery";

        public const int MinimumPermitLimit = 1;

        public static int ResolvePermitLimit(GlobalRateLimitOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return Math.Max(MinimumPermitLimit, options.PermitLimit / 2);
        }

        public static RateLimitPartition<string> CreatePartition(
            HttpContext httpContext,
            GlobalRateLimitOptions options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                GlobalRateLimitPolicy.GetPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = ResolvePermitLimit(options),
                    QueueLimit = 0,
                    Window = options.Window,
                });
        }
    }
}
