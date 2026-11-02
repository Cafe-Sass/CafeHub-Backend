using CafeHub.WebApi.Cors;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests.Cors
{
    public sealed class CorsPolicyProviderTests
    {
        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static ICorsPolicyProvider GetPolicyProvider(Dictionary<string, string?> settings)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddConfiguredCors(CreateConfiguration(settings));

            using var serviceProvider = services.BuildServiceProvider();
            return serviceProvider.GetRequiredService<ICorsPolicyProvider>();
        }

        [Fact]
        public async Task GetPolicyAsync_WithAllowedOrigin_ReturnsPolicyWithOriginAndHeaders()
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "https://cafe.example.com",
                [$"{CorsConfigurationKeys.AllowedMethodsPath}:0"] = "GET",
                [$"{CorsConfigurationKeys.AllowedMethodsPath}:1"] = "POST",
                [$"{CorsConfigurationKeys.AllowedHeadersPath}:0"] = "Authorization",
                [$"{CorsConfigurationKeys.ExposedHeadersPath}:0"] = "Retry-After",
                [CorsConfigurationKeys.AllowCredentialsPath] = "true",
                [CorsConfigurationKeys.MaxAgeSecondsPath] = "600",
            };

            var provider = GetPolicyProvider(settings);
            var context = new DefaultHttpContext { Request = { Host = new HostString("cafe.example.com") } };
            var policy = await provider.GetPolicyAsync(context, CorsExtensions.PolicyName);

            Assert.NotNull(policy);
            Assert.True(policy.SupportsCredentials);
            Assert.Equal(TimeSpan.FromSeconds(600), policy.PreflightMaxAge);
            Assert.Contains("https://cafe.example.com", policy.Origins);
            Assert.Contains("GET", policy.Methods);
            Assert.Contains("POST", policy.Methods);
            Assert.Contains("Authorization", policy.Headers);
            Assert.Contains("Retry-After", policy.ExposedHeaders);
        }

        [Fact]
        public async Task GetPolicyAsync_DisallowedOriginNotInPolicyOrigins()
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "https://cafe.example.com",
            };

            var provider = GetPolicyProvider(settings);
            var context = new DefaultHttpContext { Request = { Host = new HostString("evil.example.com") } };
            var policy = await provider.GetPolicyAsync(context, CorsExtensions.PolicyName);

            Assert.NotNull(policy);
            Assert.DoesNotContain("https://evil.example.com", policy.Origins);
        }

        [Fact]
        public async Task GetPolicyAsync_WithDefaultEmptyList_AllowsNothing()
        {
            var settings = new Dictionary<string, string?>();
            var provider = GetPolicyProvider(settings);
            var context = new DefaultHttpContext { Request = { Host = new HostString("any.example.com") } };
            var policy = await provider.GetPolicyAsync(context, CorsExtensions.PolicyName);

            Assert.NotNull(policy);
            Assert.Empty(policy.Origins);
        }

        [Fact]
        public async Task GetPolicyAsync_PreflightForAllowedOrigin_Succeeds()
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "https://cafe.example.com",
                [$"{CorsConfigurationKeys.AllowedMethodsPath}:0"] = "GET",
                [$"{CorsConfigurationKeys.AllowedMethodsPath}:1"] = "OPTIONS",
                [$"{CorsConfigurationKeys.AllowedHeadersPath}:0"] = "Authorization",
            };

            var provider = GetPolicyProvider(settings);
            var context = new DefaultHttpContext
            {
                Request = {
                    Host = new HostString("cafe.example.com"),
                    Method = "OPTIONS",
                    Headers = { ["Origin"] = "https://cafe.example.com", ["Access-Control-Request-Method"] = "GET" },
                },
            };
            var policy = await provider.GetPolicyAsync(context, CorsExtensions.PolicyName);

            Assert.NotNull(policy);
            Assert.Contains("OPTIONS", policy.Methods);
            Assert.Contains("https://cafe.example.com", policy.Origins);
        }

        [Fact]
        public void AddConfiguredCors_WithInvalidConfiguration_FailsAtRegistration()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [$"{CorsConfigurationKeys.AllowedOriginsPath}:0"] = "*",
            });

            var exception = Assert.Throws<InvalidCorsConfigurationException>(
                () => services.AddConfiguredCors(configuration));

            Assert.Contains(CorsConfigurationKeys.AllowedOriginsPath, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AddConfiguredCors_WithNullArguments_Throws()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration(new Dictionary<string, string?>());

            Assert.Throws<ArgumentNullException>(
                () => CorsExtensions.AddConfiguredCors(null!, configuration));
            Assert.Throws<ArgumentNullException>(
                () => services.AddConfiguredCors(null!));
        }
    }
}
