using CafeHub.Infrastructure.Discovery;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Discovery.Infrastructure
{
    public sealed class DiscoveryOptionsBinderTests
    {
        [Fact]
        public void Bind_MissingSection_DefaultsToTheNoneProvider()
        {
            var options = DiscoveryOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(DiscoveryProviders.None, options.Provider);
        }

        [Theory]
        [InlineData("None")]
        [InlineData("none")]
        public void Bind_NoneProvider_IsAccepted(string provider)
        {
            var options = DiscoveryOptionsBinder.Bind(Configuration(provider));

            Assert.Equal(provider, options.Provider);
        }

        [Theory]
        [InlineData("OpenAi")]
        [InlineData("Azure")]
        [InlineData("Recommendations")]
        public void Bind_UnsupportedProvider_FailsStartupWithSafeMessage(string provider)
        {
            var exception = Assert.Throws<InvalidDiscoveryConfigurationException>(
                () => DiscoveryOptionsBinder.Bind(Configuration(provider)));

            var problem = Assert.Single(exception.Problems);
            Assert.Contains(DiscoveryConfigurationKeys.ProviderPath, problem, StringComparison.Ordinal);
            Assert.Contains("Only 'None' is available", problem, StringComparison.Ordinal);
        }

        private static IConfiguration Configuration(string provider) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [DiscoveryConfigurationKeys.ProviderPath] = provider,
                })
                .Build();
    }
}
