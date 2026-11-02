using CafeHub.WebApi.Security;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Security
{
    public sealed class RequestLimitsOptionsBinderTests
    {
        private static Dictionary<string, string?> CreateValidSettings()
        {
            return new Dictionary<string, string?>
            {
                [RequestLimitsConfigurationKeys.MaxBodyBytesPath] = "1048576",
                [RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath] = "32768",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidRequestLimitsConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidRequestLimitsConfigurationException>(
                () => RequestLimitsOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithValidSettings_ReturnsTheBoundValues()
        {
            var settings = CreateValidSettings();
            settings[RequestLimitsConfigurationKeys.MaxBodyBytesPath] = "2097152";
            settings[RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath] = "65536";

            var options = RequestLimitsOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.Equal(2097152, options.MaxBodyBytes);
            Assert.Equal(65536, options.MaxHeadersTotalBytes);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefaults()
        {
            var options = RequestLimitsOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(RequestLimitsOptions.DefaultMaxBodyBytes, options.MaxBodyBytes);
            Assert.Equal(RequestLimitsOptions.DefaultMaxHeadersTotalBytes, options.MaxHeadersTotalBytes);
        }

        [Fact]
        public void Bind_WithNullConfiguration_AppliesTheDocumentedDefaults()
        {
            var options = RequestLimitsOptionsBinder.Bind(null);

            Assert.Equal(RequestLimitsOptions.DefaultMaxBodyBytes, options.MaxBodyBytes);
            Assert.Equal(RequestLimitsOptions.DefaultMaxHeadersTotalBytes, options.MaxHeadersTotalBytes);
        }

        [Theory]
        [InlineData("1023")]
        [InlineData("33554433")]
        public void Bind_WithAMaxBodyBytesOutsideTheConfiguredRange_FailsStartup(string invalidMaxBodyBytes)
        {
            var settings = CreateValidSettings();
            settings[RequestLimitsConfigurationKeys.MaxBodyBytesPath] = invalidMaxBodyBytes;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{RequestLimitsConfigurationKeys.MaxBodyBytesPath} must be between {RequestLimitsOptions.MinMaxBodyBytes} and {RequestLimitsOptions.MaxMaxBodyBytes}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidMaxBodyBytes}", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("4095")]
        [InlineData("131073")]
        public void Bind_WithAMaxHeadersTotalBytesOutsideTheConfiguredRange_FailsStartup(string invalidMaxHeadersTotalBytes)
        {
            var settings = CreateValidSettings();
            settings[RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath] = invalidMaxHeadersTotalBytes;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath} must be between {RequestLimitsOptions.MinMaxHeadersTotalBytes} and {RequestLimitsOptions.MaxMaxHeadersTotalBytes}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidMaxHeadersTotalBytes}", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonIntegerValue_FailsStartupWithoutEchoingIt()
        {
            var settings = CreateValidSettings();
            settings[RequestLimitsConfigurationKeys.MaxBodyBytesPath] = "one megabyte";

            var exception = BindInvalid(settings);

            Assert.Contains(RequestLimitsConfigurationKeys.MaxBodyBytesPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("one megabyte", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithMultipleInvalidValues_ReportsEveryProblem()
        {
            var settings = CreateValidSettings();
            settings[RequestLimitsConfigurationKeys.MaxBodyBytesPath] = "1023";
            settings[RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath] = "4095";

            var exception = BindInvalid(settings);

            Assert.Equal(2, exception.Problems.Count);
            Assert.Contains(
                exception.Problems,
                problem => problem.Contains(RequestLimitsConfigurationKeys.MaxBodyBytesPath, StringComparison.Ordinal));
            Assert.Contains(
                exception.Problems,
                problem => problem.Contains(RequestLimitsConfigurationKeys.MaxHeadersTotalBytesPath, StringComparison.Ordinal));
        }
    }
}
