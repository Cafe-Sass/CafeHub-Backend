using CafeHub.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CafeHub.UnitTests.Tenancy.Infrastructure
{
    public sealed class TenantDatabaseOptionsBinderTests
    {
        [Fact]
        public void Bind_WithMissingValues_AppliesDocumentedDefaults()
        {
            var options = TenantDatabaseOptionsBinder.Bind(Configuration("localhost"));

            Assert.Equal(5432, options.Port);
            Assert.Equal(SslMode.Prefer, options.SslMode);
            Assert.Equal(10, options.MaxPoolSize);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(70000)]
        public void Bind_WithOutOfRangePort_FailsWithoutEchoingValue(int port)
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseOptionsBinder.Bind(Configuration("localhost", port: port)));

            Assert.Equal(
                "Invalid TenantDatabases configuration: TenantDatabases:Port must be between 1 and 65535.",
                exception.Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void Bind_WithOutOfRangePoolSize_FailsWithoutEchoingValue(int poolSize)
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseOptionsBinder.Bind(Configuration("localhost", maxPoolSize: poolSize)));

            Assert.Equal(
                "Invalid TenantDatabases configuration: TenantDatabases:MaxPoolSize must be between 1 and 100.",
                exception.Message);
        }

        [Fact]
        public void Bind_WithEmptyHost_FailsWithoutEchoingConfigurationValues()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseOptionsBinder.Bind(Configuration("")));

            Assert.Equal(
                "Invalid TenantDatabases configuration: TenantDatabases:Host is required.",
                exception.Message);
        }

        [Fact]
        public void Bind_WithUnknownSslMode_FailsWithoutEchoingConfigurationValues()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["TenantDatabases:Host"] = "localhost",
                    ["TenantDatabases:SslMode"] = "UnknownMode",
                })
                .Build();

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseOptionsBinder.Bind(configuration));

            Assert.Equal(
                "TenantDatabases:SslMode must be a supported Npgsql SSL mode.",
                exception.Message);
        }

        private static IConfiguration Configuration(string host, int? port = null, int? maxPoolSize = null)
        {
            var values = new Dictionary<string, string?>
            {
                ["TenantDatabases:Host"] = host,
            };

            if (port.HasValue)
            {
                values["TenantDatabases:Port"] = port.Value.ToString();
            }

            if (maxPoolSize.HasValue)
            {
                values["TenantDatabases:MaxPoolSize"] = maxPoolSize.Value.ToString();
            }

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }
    }
}
