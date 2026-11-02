using CafeHub.WebApi.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.RateLimiting
{
    public sealed class GlobalRateLimitOptionsBinderTests
    {
        private static Dictionary<string, string?> CreateValidSettings()
        {
            return new Dictionary<string, string?>
            {
                [GlobalRateLimitConfigurationKeys.PermitLimitPath] = "300",
                [GlobalRateLimitConfigurationKeys.WindowSecondsPath] = "60",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidGlobalRateLimitConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidGlobalRateLimitConfigurationException>(
                () => GlobalRateLimitOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithValidSettings_ReturnsTheBoundValues()
        {
            var settings = CreateValidSettings();
            settings[GlobalRateLimitConfigurationKeys.PermitLimitPath] = "7";
            settings[GlobalRateLimitConfigurationKeys.WindowSecondsPath] = "30";

            var options = GlobalRateLimitOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.Equal(7, options.PermitLimit);
            Assert.Equal(30, options.WindowSeconds);
            Assert.Equal(TimeSpan.FromSeconds(30), options.Window);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefaults()
        {
            var options = GlobalRateLimitOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(GlobalRateLimitOptions.DefaultPermitLimit, options.PermitLimit);
            Assert.Equal(GlobalRateLimitOptions.DefaultWindowSeconds, options.WindowSeconds);
        }

        [Fact]
        public void Bind_WithNullConfiguration_AppliesTheDocumentedDefaults()
        {
            var options = GlobalRateLimitOptionsBinder.Bind(null);

            Assert.Equal(GlobalRateLimitOptions.DefaultPermitLimit, options.PermitLimit);
            Assert.Equal(GlobalRateLimitOptions.DefaultWindowSeconds, options.WindowSeconds);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("100001")]
        public void Bind_WithAPermitLimitOutsideTheConfiguredRange_FailsStartup(string invalidPermitLimit)
        {
            var settings = CreateValidSettings();
            settings[GlobalRateLimitConfigurationKeys.PermitLimitPath] = invalidPermitLimit;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{GlobalRateLimitConfigurationKeys.PermitLimitPath} must be between {GlobalRateLimitOptions.MinPermitLimit} and {GlobalRateLimitOptions.MaxPermitLimit}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidPermitLimit}", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("3601")]
        public void Bind_WithAWindowOutsideTheConfiguredRange_FailsStartup(string invalidWindowSeconds)
        {
            var settings = CreateValidSettings();
            settings[GlobalRateLimitConfigurationKeys.WindowSecondsPath] = invalidWindowSeconds;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{GlobalRateLimitConfigurationKeys.WindowSecondsPath} must be between {GlobalRateLimitOptions.MinWindowSeconds} and {GlobalRateLimitOptions.MaxWindowSeconds}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidWindowSeconds}", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonIntegerValue_FailsStartupWithoutEchoingIt()
        {
            var settings = CreateValidSettings();
            settings[GlobalRateLimitConfigurationKeys.PermitLimitPath] = "three hundred";

            var exception = BindInvalid(settings);

            Assert.Contains(GlobalRateLimitConfigurationKeys.PermitLimitPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("three hundred", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithMultipleInvalidValues_ReportsEveryProblem()
        {
            var settings = CreateValidSettings();
            settings[GlobalRateLimitConfigurationKeys.PermitLimitPath] = "0";
            settings[GlobalRateLimitConfigurationKeys.WindowSecondsPath] = "0";

            var exception = BindInvalid(settings);

            Assert.Equal(2, exception.Problems.Count);
            Assert.Contains(
                exception.Problems,
                problem => problem.Contains(GlobalRateLimitConfigurationKeys.PermitLimitPath, StringComparison.Ordinal));
            Assert.Contains(
                exception.Problems,
                problem => problem.Contains(GlobalRateLimitConfigurationKeys.WindowSecondsPath, StringComparison.Ordinal));
        }
    }
}
