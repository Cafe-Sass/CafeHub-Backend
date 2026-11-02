using CafeHub.WebApi.Transport;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CafeHub.UnitTests.Transport
{
    public sealed class TransportSecurityPolicyTests
    {
        [Fact]
        public void ShouldApplyHttps_DevelopmentEnvironment_ReturnsFalse()
        {
            Assert.False(TransportSecurityPolicy.ShouldApplyHttps(new FakeHostEnvironment("Development")));
        }

        [Theory]
        [InlineData("Production")]
        [InlineData("Staging")]
        [InlineData("Testing")]
        [InlineData("AnyOtherName")]
        public void ShouldApplyHttps_NonDevelopmentEnvironment_ReturnsTrue(string environmentName)
        {
            Assert.True(TransportSecurityPolicy.ShouldApplyHttps(new FakeHostEnvironment(environmentName)));
        }

        [Fact]
        public void ShouldApplyHttps_WithNullEnvironment_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => TransportSecurityPolicy.ShouldApplyHttps(null!));
        }

        private sealed class FakeHostEnvironment : IHostEnvironment
        {
            public FakeHostEnvironment(string environmentName)
            {
                EnvironmentName = environmentName;
            }

            public string EnvironmentName { get; set; }

            public string ApplicationName { get; set; } = "CafeHub.UnitTests";

            public string ContentRootPath { get; set; } = string.Empty;

            public IFileProvider ContentRootFileProvider { get; set; } = null!;
        }
    }
}
