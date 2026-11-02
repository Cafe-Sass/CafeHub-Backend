using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Builds the fixed-window partition for the global rate-limiting policy. The partition key is the
    /// direct client IP address (after forwarded headers); IPv4-mapped IPv6 addresses are normalized so the
    /// same client cannot obtain a second bucket by changing address form. A request without a remote
    /// address shares one fail-closed partition instead of receiving an unlimited one.
    /// </summary>
    public static class GlobalRateLimitPolicy
    {
        public const string UnknownPartitionKey = "unknown";

        public static RateLimitPartition<string> CreatePartition(HttpContext httpContext, GlobalRateLimitOptions options)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = options.PermitLimit,
                    QueueLimit = 0,
                    Window = options.Window,
                });
        }

        public static string GetPartitionKey(HttpContext httpContext)
        {
            if (httpContext is null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            var remoteIpAddress = httpContext.Connection.RemoteIpAddress;
            if (remoteIpAddress is null)
            {
                return UnknownPartitionKey;
            }

            if (remoteIpAddress.IsIPv4MappedToIPv6)
            {
                remoteIpAddress = remoteIpAddress.MapToIPv4();
            }

            return remoteIpAddress.ToString();
        }
    }
}
