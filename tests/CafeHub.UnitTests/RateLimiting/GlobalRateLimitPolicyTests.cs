using System.Net;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace CafeHub.UnitTests.RateLimiting
{
    public sealed class GlobalRateLimitPolicyTests
    {
        [Fact]
        public void GetPartitionKey_WithRemoteIp_ReturnsTheAddress()
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.3") } };

            var key = GlobalRateLimitPolicy.GetPartitionKey(context);

            Assert.Equal("10.1.2.3", key);
        }

        [Fact]
        public void GetPartitionKey_WithIPv4MappedToIPv6_NormalizesToIPv4()
        {
            var ipv6 = IPAddress.Parse("::ffff:10.1.2.3");
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = ipv6 } };

            var key = GlobalRateLimitPolicy.GetPartitionKey(context);

            Assert.Equal("10.1.2.3", key);
        }

        [Fact]
        public void GetPartitionKey_WithoutRemoteIp_ReturnsSharedBucket()
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = null } };

            var key = GlobalRateLimitPolicy.GetPartitionKey(context);

            Assert.Equal(GlobalRateLimitPolicy.UnknownPartitionKey, key);
        }

        [Fact]
        public void CreatePartition_WithDifferentAddresses_ReturnsIndependentPartitions()
        {
            var options = new GlobalRateLimitOptions { PermitLimit = 1, WindowSeconds = 60 };

            var context1 = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.3") } };
            var context2 = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.4") } };

            var partition1 = GlobalRateLimitPolicy.CreatePartition(context1, options);
            var partition2 = GlobalRateLimitPolicy.CreatePartition(context2, options);

            var limiter1 = partition1.Factory(partition1.PartitionKey);
            var limiter2 = partition2.Factory(partition2.PartitionKey);

            var lease1 = limiter1.AttemptAcquire();
            var lease2 = limiter2.AttemptAcquire();

            Assert.True(lease1.IsAcquired);
            Assert.True(lease2.IsAcquired);
        }

        [Fact]
        public void CreatePartition_WithSameAddress_SharesTheSameBucket()
        {
            var options = new GlobalRateLimitOptions { PermitLimit = 1, WindowSeconds = 60 };

            var context1 = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.3") } };
            var context2 = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.3") } };

            var partition = GlobalRateLimitPolicy.CreatePartition(context1, options);
            var limiter = partition.Factory(partition.PartitionKey);

            var lease1 = limiter.AttemptAcquire();
            var lease2 = limiter.AttemptAcquire();

            Assert.True(lease1.IsAcquired);
            Assert.False(lease2.IsAcquired);
        }

        [Fact]
        public void CreatePartition_WithoutRemoteIp_UsesSharedBucket()
        {
            var options = new GlobalRateLimitOptions { PermitLimit = 1, WindowSeconds = 60 };

            var context1 = new DefaultHttpContext { Connection = { RemoteIpAddress = null } };
            var context2 = new DefaultHttpContext { Connection = { RemoteIpAddress = null } };

            var partition = GlobalRateLimitPolicy.CreatePartition(context1, options);
            var limiter = partition.Factory(partition.PartitionKey);

            var lease1 = limiter.AttemptAcquire();
            var lease2 = limiter.AttemptAcquire();

            Assert.True(lease1.IsAcquired);
            Assert.False(lease2.IsAcquired);
        }

        [Fact]
        public void CreatePartition_WithNullOptions_Throws()
        {
            var context = new DefaultHttpContext { Connection = { RemoteIpAddress = IPAddress.Parse("10.1.2.3") } };

            Assert.Throws<ArgumentNullException>(() => GlobalRateLimitPolicy.CreatePartition(context, null!));
        }

        [Fact]
        public void CreatePartition_WithNullContext_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GlobalRateLimitPolicy.CreatePartition(null!, new GlobalRateLimitOptions()));
        }
    }
}
