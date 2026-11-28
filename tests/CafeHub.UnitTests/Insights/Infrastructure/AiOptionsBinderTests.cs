using CafeHub.Infrastructure.Insights;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Insights.Infrastructure
{
    public sealed class AiOptionsBinderTests
    {
        [Fact]
        public void Bind_MissingSection_DefaultsToTheNoneProvider()
        {
            var options = AiOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(AiInsightsProviders.None, options.InsightsProvider);
        }

        [Theory]
        [InlineData("None")]
        [InlineData("none")]
        public void Bind_NoneProvider_IsAccepted(string provider)
        {
            var options = AiOptionsBinder.Bind(Configuration(provider));

            Assert.Equal(provider, options.InsightsProvider);
        }

        [Theory]
        [InlineData("OpenAi")]
        [InlineData("Azure")]
        [InlineData("Log")]
        public void Bind_UnsupportedProvider_FailsStartupWithSafeMessage(string provider)
        {
            var exception = Assert.Throws<InvalidAiConfigurationException>(
                () => AiOptionsBinder.Bind(Configuration(provider)));

            var problem = Assert.Single(exception.Problems);
            Assert.Contains(AiInsightsConfigurationKeys.ProviderPath, problem, StringComparison.Ordinal);
            Assert.Contains("Only 'None' is available", problem, StringComparison.Ordinal);
        }

        private static IConfiguration Configuration(string provider) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [AiInsightsConfigurationKeys.ProviderPath] = provider,
                })
                .Build();
    }
}
