using System.Reflection;
using CafeHub.WebApi;
using CafeHub.WebApi.Controllers;
using CafeHub.WebApi.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests
{
    public sealed class AuthRateLimitingRegistrationTests
    {
        private static IConfiguration CreateConfiguration(string permitLimit, string windowSeconds)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [AuthRateLimitConfigurationKeys.PermitLimitPath] = permitLimit,
                    [AuthRateLimitConfigurationKeys.WindowSecondsPath] = windowSeconds,
                })
                .Build();
        }

        [Fact]
        public void AddAuthRateLimiting_RegistersTheBoundOptions()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddAuthRateLimiting(CreateConfiguration("7", "30"));

            using var serviceProvider = services.BuildServiceProvider();
            var options = serviceProvider.GetRequiredService<AuthRateLimitOptions>();

            Assert.Equal(7, options.PermitLimit);
            Assert.Equal(30, options.WindowSeconds);
            Assert.Equal(TimeSpan.FromSeconds(30), options.Window);
        }

        [Fact]
        public void AddAuthRateLimiting_ConfiguresThe429RejectionAndWriter()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddAuthRateLimiting(CreateConfiguration("10", "60"));

            using var serviceProvider = services.BuildServiceProvider();
            var rateLimiterOptions = serviceProvider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

            Assert.Equal(StatusCodes.Status429TooManyRequests, rateLimiterOptions.RejectionStatusCode);
            Assert.NotNull(rateLimiterOptions.OnRejected);
        }

        [Fact]
        public void AddAuthRateLimiting_WithInvalidConfiguration_FailsAtRegistration()
        {
            var services = new ServiceCollection();

            var exception = Assert.Throws<InvalidAuthRateLimitConfigurationException>(
                () => services.AddAuthRateLimiting(CreateConfiguration("0", "60")));

            Assert.Contains(AuthRateLimitConfigurationKeys.PermitLimitPath, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AddAuthRateLimiting_WithNullArguments_Throws()
        {
            var configuration = CreateConfiguration("10", "60");
            var services = new ServiceCollection();

            Assert.Throws<ArgumentNullException>(() => CafeHub.WebApi.DependencyInjection.AddAuthRateLimiting(null!, configuration));
            Assert.Throws<ArgumentNullException>(() => services.AddAuthRateLimiting(null!));
        }

        [Fact]
        public void AdminAuthController_IsCoveredByTheAdminAuthPolicy()
        {
            var attribute = typeof(AdminAuthController).GetCustomAttribute<EnableRateLimitingAttribute>();

            Assert.NotNull(attribute);
            Assert.Equal(AuthRateLimiting.PolicyName, attribute!.PolicyName);
        }
    }
}
