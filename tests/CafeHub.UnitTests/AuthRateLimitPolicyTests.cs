using System.Net;
using System.Threading.RateLimiting;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests
{
    public sealed class AuthRateLimitPolicyTests
    {
        private static DefaultHttpContext CreateHttpContext(string? remoteIpAddress)
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Connection.RemoteIpAddress = remoteIpAddress is null ? null : IPAddress.Parse(remoteIpAddress);
            return httpContext;
        }

        [Fact]
        public void GetPartitionKey_WithAnIPv4Address_ReturnsTheAddress()
        {
            var httpContext = CreateHttpContext("203.0.113.10");

            var partitionKey = AuthRateLimitPolicy.GetPartitionKey(httpContext);

            Assert.Equal("203.0.113.10", partitionKey);
        }

        [Fact]
        public void GetPartitionKey_WithAnIPv4MappedIPv6Address_ReturnsTheNormalizedIPv4Address()
        {
            var httpContext = CreateHttpContext("::ffff:203.0.113.10");

            var partitionKey = AuthRateLimitPolicy.GetPartitionKey(httpContext);

            Assert.Equal("203.0.113.10", partitionKey);
        }

        [Fact]
        public void GetPartitionKey_WithAnIPv6Address_ReturnsTheAddress()
        {
            var httpContext = CreateHttpContext("2001:db8::10");

            var partitionKey = AuthRateLimitPolicy.GetPartitionKey(httpContext);

            Assert.Equal("2001:db8::10", partitionKey);
        }

        [Fact]
        public void GetPartitionKey_WithoutARemoteAddress_ReturnsTheFailClosedUnknownKey()
        {
            var httpContext = CreateHttpContext(null);

            var partitionKey = AuthRateLimitPolicy.GetPartitionKey(httpContext);

            Assert.Equal(AuthRateLimitPolicy.UnknownPartitionKey, partitionKey);
        }

        [Fact]
        public void GetPartitionKey_WithANullContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => AuthRateLimitPolicy.GetPartitionKey(null!));
        }

        [Fact]
        public void CreatePartition_UsesTheClientIpAsThePartitionKey()
        {
            var httpContext = CreateHttpContext("198.51.100.20");
            var options = new AuthRateLimitOptions { PermitLimit = 3, WindowSeconds = 60 };

            var partition = AuthRateLimitPolicy.CreatePartition(httpContext, options);

            Assert.Equal("198.51.100.20", partition.PartitionKey);
        }

        [Fact]
        public void CreatePartition_DifferentClientIps_ProduceDifferentPartitionKeys()
        {
            var options = new AuthRateLimitOptions { PermitLimit = 3, WindowSeconds = 60 };

            var first = AuthRateLimitPolicy.CreatePartition(CreateHttpContext("198.51.100.20"), options);
            var second = AuthRateLimitPolicy.CreatePartition(CreateHttpContext("198.51.100.21"), options);

            Assert.NotEqual(first.PartitionKey, second.PartitionKey);
        }

        [Fact]
        public void CreatePartition_AllowsTheConfiguredPermitCount_ThenRejects()
        {
            var httpContext = CreateHttpContext("198.51.100.30");
            var options = new AuthRateLimitOptions { PermitLimit = 2, WindowSeconds = 60 };
            var partition = AuthRateLimitPolicy.CreatePartition(httpContext, options);
            using var limiter = partition.Factory(partition.PartitionKey);
            var acquiredLeases = new List<RateLimitLease>();

            try
            {
                for (var attempt = 0; attempt < options.PermitLimit; attempt++)
                {
                    var lease = limiter.AttemptAcquire();
                    Assert.True(lease.IsAcquired, $"Attempt {attempt + 1} should be admitted.");
                    acquiredLeases.Add(lease);
                }

                using var rejectedLease = limiter.AttemptAcquire();
                Assert.False(rejectedLease.IsAcquired);
            }
            finally
            {
                foreach (var lease in acquiredLeases)
                {
                    lease.Dispose();
                }
            }
        }

        [Fact]
        public void CreatePartition_WithNullArguments_Throws()
        {
            var httpContext = CreateHttpContext("198.51.100.40");
            var options = new AuthRateLimitOptions();

            Assert.Throws<ArgumentNullException>(() => AuthRateLimitPolicy.CreatePartition(null!, options));
            Assert.Throws<ArgumentNullException>(() => AuthRateLimitPolicy.CreatePartition(httpContext, null!));
        }
    }
}
