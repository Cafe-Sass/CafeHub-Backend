using System.Net;
using CafeHub.WebApi.Transport;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Transport
{
    public sealed class TransportSecurityOptionsBinderTests
    {
        private static Dictionary<string, string?> CreateTemplateDefaults()
        {
            return new Dictionary<string, string?>
            {
                [TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath] = "365",
                [TransportSecurityConfigurationKeys.HstsIncludeSubDomainsPath] = "false",
                [TransportSecurityConfigurationKeys.HstsPreloadPath] = "false",
                [TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "false",
                [TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath] = "1",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidTransportSecurityConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidTransportSecurityConfigurationException>(
                () => TransportSecurityOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithTemplateDefaults_ReturnsTheDocumentedDefaults()
        {
            var options = TransportSecurityOptionsBinder.Bind(CreateConfiguration(CreateTemplateDefaults()));

            Assert.Equal(365, options.Hsts.MaxAgeDays);
            Assert.False(options.Hsts.IncludeSubDomains);
            Assert.False(options.Hsts.Preload);
            Assert.Equal(TimeSpan.FromDays(365), options.HstsMaxAge);
            Assert.Null(options.HttpsPort);
            Assert.False(options.ForwardedHeaders.Enabled);
            Assert.Equal(1, options.ForwardedHeaders.ForwardLimit);
            Assert.Empty(options.ForwardedHeaders.KnownProxies);
            Assert.Empty(options.ForwardedHeaders.KnownNetworks);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefaults()
        {
            var options = TransportSecurityOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(TransportSecurityOptions.DefaultHstsMaxAgeDays, options.Hsts.MaxAgeDays);
            Assert.False(options.Hsts.IncludeSubDomains);
            Assert.False(options.Hsts.Preload);
            Assert.Null(options.HttpsPort);
            Assert.False(options.ForwardedHeaders.Enabled);
            Assert.Equal(TransportSecurityOptions.DefaultForwardLimit, options.ForwardedHeaders.ForwardLimit);
            Assert.Empty(options.ForwardedHeaders.KnownProxies);
            Assert.Empty(options.ForwardedHeaders.KnownNetworks);
        }

        [Fact]
        public void Bind_WithNullConfiguration_AppliesTheDocumentedDefaults()
        {
            var options = TransportSecurityOptionsBinder.Bind(null);

            Assert.Equal(TransportSecurityOptions.DefaultHstsMaxAgeDays, options.Hsts.MaxAgeDays);
            Assert.False(options.ForwardedHeaders.Enabled);
        }

        [Fact]
        public void Bind_WithAValidProxyAndNetwork_ReturnsThem()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "true";
            settings[$"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownProxiesPath}:0"] = "10.1.2.3";
            settings[$"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownNetworksPath}:0"] = "192.168.0.0/16";

            var options = TransportSecurityOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.True(options.ForwardedHeaders.Enabled);
            Assert.Equal(IPAddress.Parse("10.1.2.3"), Assert.Single(options.ForwardedHeaders.KnownProxies));
            var network = Assert.Single(options.ForwardedHeaders.KnownNetworks);
            Assert.Equal(IPAddress.Parse("192.168.0.0"), network.BaseAddress);
            Assert.Equal(16, network.PrefixLength);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("731")]
        public void Bind_WithAMaxAgeOutsideTheConfiguredRange_FailsStartup(string invalidMaxAgeDays)
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath] = invalidMaxAgeDays;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath} must be between {TransportSecurityOptions.MinHstsMaxAgeDays} and {TransportSecurityOptions.MaxHstsMaxAgeDays}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidMaxAgeDays}", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("65536")]
        public void Bind_WithAnHttpsPortOutsideTheConfiguredRange_FailsStartup(string invalidPort)
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.HttpsPortPath] = invalidPort;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{TransportSecurityConfigurationKeys.HttpsPortPath} must be between {TransportSecurityOptions.MinHttpsPort} and {TransportSecurityOptions.MaxHttpsPort}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidPort}", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("6")]
        public void Bind_WithAForwardLimitOutsideTheConfiguredRange_FailsStartup(string invalidForwardLimit)
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath] = invalidForwardLimit;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath} must be between {TransportSecurityOptions.MinForwardLimit} and {TransportSecurityOptions.MaxForwardLimit}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidForwardLimit}", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithAMalformedProxy_FailsStartupWithoutEchoingIt()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "true";
            settings[$"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownProxiesPath}:0"] = "10.0.0.999";

            var exception = BindInvalid(settings);

            Assert.Contains(TransportSecurityConfigurationKeys.ForwardedHeadersKnownProxiesPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("10.0.0.999", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithAMalformedNetwork_FailsStartupWithoutEchoingIt()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "true";
            settings[$"{TransportSecurityConfigurationKeys.ForwardedHeadersKnownNetworksPath}:0"] = "10.0.0.0/99";

            var exception = BindInvalid(settings);

            Assert.Contains(TransportSecurityConfigurationKeys.ForwardedHeadersKnownNetworksPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("10.0.0.0/99", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithForwardingEnabledAndNoTrust_FailsStartup()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath] = "true";

            var exception = BindInvalid(settings);

            Assert.Contains(TransportSecurityConfigurationKeys.ForwardedHeadersEnabledPath, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithAnInvalidBoolean_FailsStartup()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.HstsIncludeSubDomainsPath] = "sometimes";

            var exception = BindInvalid(settings);

            Assert.Contains(TransportSecurityConfigurationKeys.HstsIncludeSubDomainsPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("sometimes", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonIntegerValue_FailsStartupWithoutEchoingIt()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.HttpsPortPath] = "eight thousand";

            var exception = BindInvalid(settings);

            Assert.Contains(TransportSecurityConfigurationKeys.HttpsPortPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("eight thousand", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithMultipleInvalidValues_ReportsEveryProblem()
        {
            var settings = CreateTemplateDefaults();
            settings[TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath] = "0";
            settings[TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath] = "6";

            var exception = BindInvalid(settings);

            Assert.Equal(2, exception.Problems.Count);
            Assert.Contains(
                exception.Problems,
                problem => problem.Contains(TransportSecurityConfigurationKeys.HstsMaxAgeDaysPath, StringComparison.Ordinal));
            Assert.Contains(
                exception.Problems,
                problem => problem.Contains(TransportSecurityConfigurationKeys.ForwardedHeadersForwardLimitPath, StringComparison.Ordinal));
        }
    }
}
