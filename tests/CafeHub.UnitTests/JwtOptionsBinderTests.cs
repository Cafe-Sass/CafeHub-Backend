using CafeHub.Application.Identity;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class JwtOptionsBinderTests
    {
        private const string ValidSigningKey = "0123456789abcdef0123456789abcdef";

        private static Dictionary<string, string?> CreateValidSettings()
        {
            return new Dictionary<string, string?>
            {
                [JwtConfigurationKeys.IssuerPath] = "https://issuer.cafehub.test",
                [JwtConfigurationKeys.AudiencePath] = "cafehub-api",
                [JwtConfigurationKeys.SigningKeyPath] = ValidSigningKey,
                [JwtConfigurationKeys.AccessTokenLifetimeMinutesPath] = "15",
                [JwtConfigurationKeys.ClockSkewSecondsPath] = "30",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidJwtConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidJwtConfigurationException>(() => JwtOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithValidSettings_ReturnsTheBoundValues()
        {
            var configuration = CreateConfiguration(CreateValidSettings());

            var options = JwtOptionsBinder.Bind(configuration);

            Assert.Equal("https://issuer.cafehub.test", options.Issuer);
            Assert.Equal("cafehub-api", options.Audience);
            Assert.Equal(ValidSigningKey, options.SigningKey);
            Assert.Equal(15, options.AccessTokenLifetimeMinutes);
            Assert.Equal(30, options.ClockSkewSeconds);
        }

        [Fact]
        public void Bind_WithMissingSection_FailsClosed_WithTheDocumentedDefaultLifetime()
        {
            var configuration = new ConfigurationBuilder().Build();

            var exception = Record.Exception(() => JwtOptionsBinder.Bind(configuration));

            Assert.IsType<InvalidJwtConfigurationException>(exception);
            Assert.Contains(JwtConfigurationKeys.IssuerPath, exception!.Message, StringComparison.Ordinal);
            Assert.Contains(JwtConfigurationKeys.AudiencePath, exception!.Message, StringComparison.Ordinal);
            Assert.Contains(JwtConfigurationKeys.SigningKeyPath, exception!.Message, StringComparison.Ordinal);
            Assert.DoesNotContain($"{JwtConfigurationKeys.AccessTokenLifetimeMinutesPath} must", exception!.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("0123456789abcdef0123456789abcde")]
        public void Bind_WithMissingEmptyOrShortSigningKey_FailsStartupWithoutPrintingTheKey(string invalidKey)
        {
            var settings = CreateValidSettings();
            settings[JwtConfigurationKeys.SigningKeyPath] = invalidKey;

            var exception = BindInvalid(settings);

            Assert.Contains(JwtConfigurationKeys.SigningKeyPath, exception.Message, StringComparison.Ordinal);
            if (!string.IsNullOrWhiteSpace(invalidKey))
            {
                Assert.DoesNotContain(invalidKey, exception.Message, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void Bind_WithEmptyIssuer_FailsStartup()
        {
            var settings = CreateValidSettings();
            settings[JwtConfigurationKeys.IssuerPath] = "";

            var exception = BindInvalid(settings);

            Assert.Contains($"{JwtConfigurationKeys.IssuerPath} must not be empty", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithEmptyAudience_FailsStartup()
        {
            var settings = CreateValidSettings();
            settings[JwtConfigurationKeys.AudiencePath] = "";

            var exception = BindInvalid(settings);

            Assert.Contains($"{JwtConfigurationKeys.AudiencePath} must not be empty", exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("61")]
        [InlineData("600")]
        public void Bind_WithLifetimeOutsideTheOneToSixtyMinuteRange_FailsStartup(string invalidLifetime)
        {
            var settings = CreateValidSettings();
            settings[JwtConfigurationKeys.AccessTokenLifetimeMinutesPath] = invalidLifetime;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{JwtConfigurationKeys.AccessTokenLifetimeMinutesPath} must be between {JwtOptions.MinAccessTokenLifetimeMinutes} and {JwtOptions.MaxAccessTokenLifetimeMinutes}",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithNonIntegerLifetime_FailsStartup()
        {
            var settings = CreateValidSettings();
            settings[JwtConfigurationKeys.AccessTokenLifetimeMinutesPath] = "abc";

            var exception = BindInvalid(settings);

            Assert.Contains($"{JwtConfigurationKeys.AccessTokenLifetimeMinutesPath} is not a valid 32-bit integer", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithValidSigningKey_AppliesTheDocumentedDefaults()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [JwtConfigurationKeys.IssuerPath] = "https://issuer.cafehub.test",
                [JwtConfigurationKeys.AudiencePath] = "cafehub-api",
                [JwtConfigurationKeys.SigningKeyPath] = ValidSigningKey,
            });

            var options = JwtOptionsBinder.Bind(configuration);

            Assert.Equal(JwtOptions.DefaultAccessTokenLifetimeMinutes, options.AccessTokenLifetimeMinutes);
            Assert.Equal(JwtOptions.DefaultClockSkewSeconds, options.ClockSkewSeconds);
        }

        [Fact]
        public void Bind_FailureMessageNamesTheEnvironmentVariable_AndNeverPrintsTheKeyValue()
        {
            const string secretLikeKey = "sk-do-not-leak-me-0123456789";
            var settings = CreateValidSettings();
            settings[JwtConfigurationKeys.SigningKeyPath] = secretLikeKey;

            var exception = BindInvalid(settings);

            Assert.Contains(JwtConfigurationKeys.SigningKeyEnvironmentVariable, exception.Message, StringComparison.Ordinal);
            Assert.Contains($"{JwtConfigurationKeys.SigningKeyPath} must be at least {JwtOptions.MinSigningKeyBytes} bytes", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(secretLikeKey, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolvingAccessTokenIssuer_FailsClosed_WhenJwtConfigurationIsInvalid()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [JwtConfigurationKeys.SigningKeyPath] = "0123456789abcdef0123456789abcde",
            });
            var services = new ServiceCollection();
            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<IAccessTokenIssuer>());

            Assert.IsType<InvalidJwtConfigurationException>(exception);
        }
    }
}
