using System;
using System.Reflection;
using CafeHub.WebApi;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests.RateLimiting
{
    public sealed class GlobalRateLimitingRegistrationTests
    {
        private static IConfiguration CreateConfiguration(string permitLimit, string windowSeconds)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [GlobalRateLimitConfigurationKeys.PermitLimitPath] = permitLimit,
                    [GlobalRateLimitConfigurationKeys.WindowSecondsPath] = windowSeconds,
                })
                .Build();
        }

        [Fact]
        public void AddGlobalRateLimiting_RegistersTheBoundOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddGlobalRateLimiting(CreateConfiguration("7", "30"));

            using var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<GlobalRateLimitOptions>();

            Assert.Equal(7, options.PermitLimit);
            Assert.Equal(30, options.WindowSeconds);
            Assert.Equal(TimeSpan.FromSeconds(30), options.Window);
        }

        [Fact]
        public void AddGlobalRateLimiting_ConfiguresTheGlobalPolicy()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddGlobalRateLimiting(CreateConfiguration("100", "60"));

            using var serviceProvider = services.BuildServiceProvider();
            var rateLimiterOptions = serviceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

            Assert.NotNull(rateLimiterOptions.OnRejected);
            Assert.Equal(StatusCodes.Status429TooManyRequests, rateLimiterOptions.RejectionStatusCode);
        }

        [Fact]
        public void AddGlobalRateLimiting_DoesNotReplaceTheAuthPolicy()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddAuthRateLimiting(CreateConfiguration("10", "60"));
            services.AddGlobalRateLimiting(CreateConfiguration("100", "60"));

            using var serviceProvider = services.BuildServiceProvider();
            var rateLimiterOptions = serviceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

            Assert.NotNull(rateLimiterOptions.OnRejected);
            Assert.Equal(StatusCodes.Status429TooManyRequests, rateLimiterOptions.RejectionStatusCode);
        }

        [Fact]
        public void AddGlobalRateLimiting_WithInvalidConfiguration_FailsAtRegistration()
        {
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidGlobalRateLimitConfigurationException>(
                () => services.AddGlobalRateLimiting(CreateConfiguration("0", "60")));

            Assert.Contains(GlobalRateLimitConfigurationKeys.PermitLimitPath, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AddGlobalRateLimiting_WithNullArguments_Throws()
        {
            var services = new ServiceCollection();
            var configuration = CreateConfiguration("100", "60");

            Assert.Throws<ArgumentNullException>(
                () => DependencyInjection.AddGlobalRateLimiting(null!, configuration));
            Assert.Throws<ArgumentNullException>(
                () => services.AddGlobalRateLimiting(null!));
        }

        [Fact]
        public void AdminAuthController_IsStillCoveredByTheAdminAuthPolicy()
        {
            var attribute = typeof(AdminAuthController).GetCustomAttribute<EnableRateLimitingAttribute>();

            Assert.NotNull(attribute);
            Assert.Equal(AuthRateLimiting.PolicyName, attribute!.PolicyName);
        }
    }
}
