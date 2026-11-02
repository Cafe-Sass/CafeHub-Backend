using CafeHub.WebApi.Cors;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Cors
{
    public sealed class CorsOptionsBinderTests
    {
        private static Dictionary<string, string?> CreateTemplateDefaults()
        {
            return new Dictionary<string, string?>
            {
                [CorsConfigurationKeys.AllowedMethodsPath] = "GET, POST, PUT, PATCH",
                [CorsConfigurationKeys.AllowedHeadersPath] = "Authorization, Content-Type, Idempotency-Key, X-Correlation-Id",
                [CorsConfigurationKeys.ExposedHeadersPath] = "X-Correlation-Id, Retry-After",
                [CorsConfigurationKeys.AllowCredentialsPath] = "false",
                [CorsConfigurationKeys.MaxAgeSecondsPath] = "600",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidCorsConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidCorsConfigurationException>(
                () => CorsOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithTemplateDefaults_ReturnsTheDocumentedDefaults()
        {
            var options = CorsOptionsBinder.Bind(CreateConfiguration(CreateTemplateDefaults()));

            Assert.Empty(options.AllowedOrigins);
            Assert.Equal(new[] { "GET", "POST", "PUT", "PATCH" }, options.AllowedMethods);
            Assert.Equal(new[] { "Authorization", "Content-Type", "Idempotency-Key", "X-Correlation-Id" }, options.AllowedHeaders);
            Assert.Equal(new[] { "X-Correlation-Id", "Retry-After" }, options.ExposedHeaders);
            Assert.False(options.AllowCredentials);
            Assert.Equal(600, options.MaxAgeSeconds);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefaults()
        {
            var options = CorsOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Empty(options.AllowedOrigins);
            Assert.False(options.AllowCredentials);
            Assert.Equal(CorsOptions.DefaultMaxAgeSeconds, options.MaxAgeSeconds);
        }

        [Fact]
        public void Bind_WithNullConfiguration_AppliesTheDocumentedDefaults()
        {
            var options = CorsOptionsBinder.Bind(null);

            Assert.Empty(options.AllowedOrigins);
            Assert.False(options.AllowCredentials);
        }

        [Fact]
        public void Bind_WithAllowedOrigins_ReturnsThem()
        {
            var settings = CreateTemplateDefaults();
            settings[$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "https://cafe.example.com";
            settings[$"{CorsConfigurationKeys.AllowedOriginsPath}:1"] = "http://localhost:5000";

            var options = CorsOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.Equal(2, options.AllowedOrigins.Count);
            Assert.Contains("https://cafe.example.com", options.AllowedOrigins);
            Assert.Contains("http://localhost:5000", options.AllowedOrigins);
        }

        [Fact]
        public void Bind_WithAWildcardOrigin_FailsStartup()
        {
            var settings = CreateTemplateDefaults();
            settings[$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "*";

            var exception = BindInvalid(settings);

            Assert.Contains(CorsConfigurationKeys.AllowedOriginsPath, exception.Message, StringComparison.Ordinal);
            Assert.Contains("wildcard", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("*", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithCredentialsAndNoOrigins_FailsStartup()
        {
            var settings = CreateTemplateDefaults();
            settings[CorsConfigurationKeys.AllowCredentialsPath] = "true";

            var exception = BindInvalid(settings);

            Assert.Contains(CorsConfigurationKeys.AllowCredentialsPath, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("cafe.example.com")]
        [InlineData("https://cafe.example.com/path")]
        [InlineData("https://cafe.example.com?query=1")]
        [InlineData("https://cafe.example.com/")]
        [InlineData("ftp://cafe.example.com")]
        public void Bind_WithAnInvalidOrigin_FailsStartup(string invalidOrigin)
        {
            var settings = CreateTemplateDefaults();
            settings[$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = invalidOrigin;

            var exception = BindInvalid(settings);

            Assert.Contains(CorsConfigurationKeys.AllowedOriginsPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(invalidOrigin, exception.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("86401")]
        public void Bind_WithAMaxAgeOutsideTheConfiguredRange_FailsStartup(string invalidMaxAgeSeconds)
        {
            var settings = CreateTemplateDefaults();
            settings[CorsConfigurationKeys.MaxAgeSecondsPath] = invalidMaxAgeSeconds;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{CorsConfigurationKeys.MaxAgeSecondsPath} must be between {CorsOptions.MinMaxAgeSeconds} and {CorsOptions.MaxMaxAgeSeconds}",
                exception.Message,
                StringComparison.Ordinal);
            Assert.DoesNotContain($"but was {invalidMaxAgeSeconds}", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithMultipleInvalidValues_ReportsEveryProblem()
        {
            var settings = CreateTemplateDefaults();
            settings[$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "*";
            settings[$"{CorsConfigurationKeys.AllowedOriginsPath}:1"] = "not-a-uri";

            var exception = BindInvalid(settings);

            Assert.True(exception.Problems.Count >= 2);
            Assert.All(
                exception.Problems,
                problem => Assert.Contains(CorsConfigurationKeys.AllowedOriginsPath, problem, StringComparison.Ordinal));
        }

        [Fact]
        public void Bind_WithANonIntegerMaxAge_FailsStartupWithoutEchoingIt()
        {
            var settings = CreateTemplateDefaults();
            settings[CorsConfigurationKeys.MaxAgeSecondsPath] = "ten minutes";

            var exception = BindInvalid(settings);

            Assert.Contains(CorsConfigurationKeys.MaxAgeSecondsPath, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("ten minutes", exception.Message, StringComparison.Ordinal);
        }
    }
}
