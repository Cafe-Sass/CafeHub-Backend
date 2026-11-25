using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using CafeHub.WebApi.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Fixed-window limits for connector requests, partitioned by the SHA-256 form of the presented key so
    /// one connector cannot exhaust another's window and the raw key never becomes a partition key. A
    /// request without a key shares one fail-closed partition. The limit is enforced by the connector
    /// authorization filter because the global controller convention overrides endpoint policy metadata.
    /// </summary>
    public static class ConnectorRateLimiting
    {
        public const int PermitLimit = 120;
        public const int WindowSeconds = 60;
        public const string UnknownPartitionKey = "unknown";

        public static string GetPartitionKey(HttpContext httpContext)
        {
            ArgumentNullException.ThrowIfNull(httpContext);

            var presentedKey = httpContext.Request.Headers[ConnectorKeyAuthorizationFilter.HeaderName].ToString();
            return string.IsNullOrEmpty(presentedKey)
                ? UnknownPartitionKey
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(presentedKey)))
                    .ToLowerInvariant();
        }

        public static ObjectResult CreateRejection(HttpContext httpContext, RateLimitLease lease) =>
            GlobalRateLimitRejectionWriter.CreateResult(httpContext, lease);
    }

    public sealed class ConnectorRateLimiter : IDisposable
    {
        private readonly PartitionedRateLimiter<string> limiter =
            PartitionedRateLimiter.Create<string, string>(partitionKey =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = ConnectorRateLimiting.PermitLimit,
                        QueueLimit = 0,
                        Window = TimeSpan.FromSeconds(ConnectorRateLimiting.WindowSeconds),
                    }));

        public RateLimitLease Acquire(string partitionKey) => limiter.AttemptAcquire(partitionKey);

        public void Dispose() => limiter.Dispose();
    }
}
