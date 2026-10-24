using CafeHub.Application.Identity;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class RefreshTokenOptionsBinderTests
    {
        private static Dictionary<string, string?> CreateValidSettings()
        {
            return new Dictionary<string, string?>
            {
                [RefreshTokenConfigurationKeys.LifetimeDaysPath] = "7",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidRefreshTokenConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidRefreshTokenConfigurationException>(() => RefreshTokenOptionsBinder.Bind(configuration));
        }

        [Fact]
        public void Bind_WithAValidLifetime_ReturnsTheBoundValue()
        {
            var configuration = CreateConfiguration(CreateValidSettings());

            var options = RefreshTokenOptionsBinder.Bind(configuration);

            Assert.Equal(7, options.LifetimeDays);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefault()
        {
            var configuration = new ConfigurationBuilder().Build();

            var options = RefreshTokenOptionsBinder.Bind(configuration);

            Assert.Equal(RefreshTokenOptions.DefaultLifetimeDays, options.LifetimeDays);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("91")]
        [InlineData("900")]
        public void Bind_WithALifetimeOutsideTheOneToNinetyDayRange_FailsStartup(string invalidLifetime)
        {
            var settings = CreateValidSettings();
            settings[RefreshTokenConfigurationKeys.LifetimeDaysPath] = invalidLifetime;

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{RefreshTokenConfigurationKeys.LifetimeDaysPath} must be between {RefreshTokenOptions.MinLifetimeDays} and {RefreshTokenOptions.MaxLifetimeDays}",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonIntegerLifetime_FailsStartup()
        {
            var settings = CreateValidSettings();
            settings[RefreshTokenConfigurationKeys.LifetimeDaysPath] = "abc";

            var exception = BindInvalid(settings);

            Assert.Contains($"{RefreshTokenConfigurationKeys.LifetimeDaysPath} is not a valid 32-bit integer", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolvingRefreshTokenService_FailsClosed_WhenTheConfigurationIsInvalid()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [ConfigurationKeys.MasterConnectionString] = "Host=localhost;Port=5432;Database=tests",
                [RefreshTokenConfigurationKeys.LifetimeDaysPath] = "0",
            });
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging();
            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<IRefreshTokenService>());

            Assert.IsType<InvalidRefreshTokenConfigurationException>(exception);
        }
    }
}
