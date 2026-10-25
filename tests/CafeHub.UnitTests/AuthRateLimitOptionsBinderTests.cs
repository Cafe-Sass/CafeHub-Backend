using CafeHub.WebApi.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests
{
    public sealed class AuthRateLimitOptionsBinderTests
    {
        private static Dictionary<string, string?> CreateValidSettings()
        {
            return new Dictionary<string, string?>
            {
                [AuthRateLimitConfigurationKeys.PermitLimitPath] = "10",
                [AuthRateLimitConfigurationKeys.WindowSecondsPath] = "60",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidAuthRateLimitConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidAuthRateLimitConfigurationException>(() => AuthRateLimitOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithValidSettings_ReturnsTheBoundValues()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [AuthRateLimitConfigurationKeys.PermitLimitPath] = "7",
                [AuthRateLimitConfigurationKeys.WindowSecondsPath] = "30",
            });

            var options = AuthRateLimitOptionsBinder.Bind(configuration);

            Assert.Equal(7, options.PermitLimit);
            Assert.Equal(30, options.WindowSeconds);
            Assert.Equal(TimeSpan.FromSeconds(30), options.Window);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefaults()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = AuthRateLimitOptionsBinder.Bind(configuration);

            Assert.Equal(AuthRateLimitOptions.DefaultPermitLimit, options.PermitLimit);
            Assert.Equal(AuthRateLimitOptions.DefaultWindowSeconds, options.WindowSeconds);
        }

        [Fact]
        public void Bind_WithNullConfiguration_AppliesTheDocumentedDefaults()
        {
            var options = AuthRateLimitOptionsBinder.Bind(null);

            Assert.Equal(AuthRateLimitOptions.DefaultPermitLimit, options.PermitLimit);
            Assert.Equal(AuthRateLimitOptions.DefaultWindowSeconds, options.WindowSeconds);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("1001")]
        public void Bind_WithAPermitLimitOutsideTheConfiguredRange_FailsStartup(string invalidPermitLimit)
        {
            var settings = CreateValidSettings();
            settings[AuthRateLimitConfigurationKeys.PermitLimitPath] = invalidPermitLimit;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{AuthRateLimitConfigurationKeys.PermitLimitPath} must be between {AuthRateLimitOptions.MinPermitLimit} and {AuthRateLimitOptions.MaxPermitLimit}",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("3601")]
        public void Bind_WithAWindowOutsideTheConfiguredRange_FailsStartup(string invalidWindowSeconds)
        {
            var settings = CreateValidSettings();
            settings[AuthRateLimitConfigurationKeys.WindowSecondsPath] = invalidWindowSeconds;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{AuthRateLimitConfigurationKeys.WindowSecondsPath} must be between {AuthRateLimitOptions.MinWindowSeconds} and {AuthRateLimitOptions.MaxWindowSeconds}",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonIntegerPermitLimit_FailsStartup()
        {
            var settings = CreateValidSettings();
            settings[AuthRateLimitConfigurationKeys.PermitLimitPath] = "ten";

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{AuthRateLimitConfigurationKeys.PermitLimitPath} is not a valid 32-bit integer",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonIntegerWindow_FailsStartup()
        {
            var settings = CreateValidSettings();
            settings[AuthRateLimitConfigurationKeys.WindowSecondsPath] = "a minute";

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{AuthRateLimitConfigurationKeys.WindowSecondsPath} is not a valid 32-bit integer",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithMultipleInvalidValues_ReportsEveryProblem()
        {
            var settings = CreateValidSettings();
            settings[AuthRateLimitConfigurationKeys.PermitLimitPath] = "0";
            settings[AuthRateLimitConfigurationKeys.WindowSecondsPath] = "0";

            var exception = BindInvalid(settings);

            Assert.Equal(2, exception.Problems.Count);
            Assert.Contains(exception.Problems, problem => problem.Contains(AuthRateLimitConfigurationKeys.PermitLimitPath, StringComparison.Ordinal));
            Assert.Contains(exception.Problems, problem => problem.Contains(AuthRateLimitConfigurationKeys.WindowSecondsPath, StringComparison.Ordinal));
        }
    }
}
