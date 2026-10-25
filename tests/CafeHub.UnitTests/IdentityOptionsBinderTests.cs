using CafeHub.Application.Identity;
using CafeHub.Domain.Identity;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Identity;
using CafeHub.Infrastructure.Persistence.Master.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CafeHub.UnitTests
{
    public sealed class IdentityOptionsBinderTests
    {
        private const string TestMasterConnection = "Host=localhost;Port=5432;Database=tests";

        private static Dictionary<string, string?> CreateValidSettings()
        {
            return new Dictionary<string, string?>
            {
                [ConfigurationKeys.MasterConnectionString] = TestMasterConnection,
                [IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = "5",
                [IdentityConfigurationKeys.LockoutMinutesPath] = "15",
            };
        }

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private static InvalidIdentityConfigurationException BindInvalid(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);

            return Assert.Throws<InvalidIdentityConfigurationException>(() => IdentityOptionsBinder.Bind(configuration));
        }

        private static ServiceProvider BuildServiceProvider(Dictionary<string, string?> settings)
        {
            var configuration = CreateConfiguration(settings);
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);

            return services.BuildServiceProvider();
        }

        [Fact]
        public void Bind_WithConfiguredThresholds_ReturnsTheBoundValues()
        {
            var settings = CreateValidSettings();
            settings[IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = "7";
            settings[IdentityConfigurationKeys.LockoutMinutesPath] = "30";

            var options = IdentityOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.Equal(7, options.MaxFailedAccessAttempts);
            Assert.Equal(30, options.LockoutMinutes);
        }

        [Fact]
        public void Bind_WithAMissingSection_AppliesTheDocumentedDefaults()
        {
            var options = IdentityOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Equal(IdentityLockoutOptions.DefaultMaxFailedAccessAttempts, options.MaxFailedAccessAttempts);
            Assert.Equal(IdentityLockoutOptions.DefaultLockoutMinutes, options.LockoutMinutes);
        }

        [Fact]
        public void Bind_WithOnlyOneThresholdConfigured_KeepsTheOtherAtItsDocumentedDefault()
        {
            var settings = new Dictionary<string, string?>
            {
                [IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = "4",
            };

            var options = IdentityOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.Equal(4, options.MaxFailedAccessAttempts);
            Assert.Equal(IdentityLockoutOptions.DefaultLockoutMinutes, options.LockoutMinutes);
        }

        [Theory]
        [InlineData("2")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("21")]
        [InlineData("100")]
        public void Bind_WithAFailedAttemptLimitOutsideTheThreeToTwentyRange_FailsStartup(string invalidAttempts)
        {
            var settings = new Dictionary<string, string?>
            {
                [IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = invalidAttempts,
            };

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{IdentityConfigurationKeys.MaxFailedAccessAttemptsPath} must be between {IdentityLockoutOptions.MinMaxFailedAccessAttempts} and {IdentityLockoutOptions.MaxMaxFailedAccessAttempts} but was {invalidAttempts}.",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("1441")]
        [InlineData("100000")]
        public void Bind_WithALockoutDurationOutsideTheBoundRange_FailsStartup(string invalidMinutes)
        {
            var settings = new Dictionary<string, string?>
            {
                [IdentityConfigurationKeys.LockoutMinutesPath] = invalidMinutes,
            };

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{IdentityConfigurationKeys.LockoutMinutesPath} must be between {IdentityLockoutOptions.MinLockoutMinutes} and {IdentityLockoutOptions.MaxLockoutMinutes} but was {invalidMinutes}.",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("3", "1")]
        [InlineData("20", "1440")]
        [InlineData("3", "1440")]
        [InlineData("20", "1")]
        public void Bind_AcceptsTheWidestConfiguredThresholds(string attempts, string lockoutMinutes)
        {
            var settings = new Dictionary<string, string?>
            {
                [IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = attempts,
                [IdentityConfigurationKeys.LockoutMinutesPath] = lockoutMinutes,
            };

            var options = IdentityOptionsBinder.Bind(CreateConfiguration(settings));

            Assert.Equal(int.Parse(attempts, System.Globalization.CultureInfo.InvariantCulture), options.MaxFailedAccessAttempts);
            Assert.Equal(int.Parse(lockoutMinutes, System.Globalization.CultureInfo.InvariantCulture), options.LockoutMinutes);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("5.0")]
        public void Bind_WithANonIntegerFailedAttemptValue_FailsStartup(string invalidAttempts)
        {
            var settings = new Dictionary<string, string?>
            {
                [IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = invalidAttempts,
            };

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{IdentityConfigurationKeys.SectionName}:{IdentityConfigurationKeys.LockoutKey}:{IdentityConfigurationKeys.MaxFailedAccessAttemptsKey} is not a valid 32-bit integer.",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithTwoInvalidThresholds_ReportsBothProblems()
        {
            var settings = new Dictionary<string, string?>
            {
                [IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = "2",
                [IdentityConfigurationKeys.LockoutMinutesPath] = "0",
            };

            var exception = BindInvalid(settings);

            Assert.Contains(
                $"{IdentityConfigurationKeys.MaxFailedAccessAttemptsPath} must be between {IdentityLockoutOptions.MinMaxFailedAccessAttempts} and {IdentityLockoutOptions.MaxMaxFailedAccessAttempts} but was 2.",
                exception.Message,
                StringComparison.Ordinal);
            Assert.Contains(
                $"{IdentityConfigurationKeys.LockoutMinutesPath} must be between {IdentityLockoutOptions.MinLockoutMinutes} and {IdentityLockoutOptions.MaxLockoutMinutes} but was 0.",
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void AddInfrastructure_WithAnInvalidLockoutConfiguration_FailsClosed()
        {
            var settings = CreateValidSettings();
            settings[IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = "2";
            var services = new ServiceCollection();

            var exception = Record.Exception(() =>
            {
                var configuration = CreateConfiguration(settings);
                services.AddSingleton<IConfiguration>(configuration);
                services.AddInfrastructure(configuration);
            });

            Assert.IsType<InvalidIdentityConfigurationException>(exception);
            Assert.Contains(
                $"{IdentityConfigurationKeys.MaxFailedAccessAttemptsPath} must be between 3 and 20 but was 2.",
                exception!.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void AddInfrastructure_ConfiguresTheIdentityLockoutFromTheBoundThresholds()
        {
            var settings = CreateValidSettings();
            settings[IdentityConfigurationKeys.MaxFailedAccessAttemptsPath] = "7";
            settings[IdentityConfigurationKeys.LockoutMinutesPath] = "30";

            using var serviceProvider = BuildServiceProvider(settings);

            var identityOptions = serviceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value;

            Assert.Equal(7, identityOptions.Lockout.MaxFailedAccessAttempts);
            Assert.Equal(TimeSpan.FromMinutes(30), identityOptions.Lockout.DefaultLockoutTimeSpan);
            Assert.True(identityOptions.Lockout.AllowedForNewUsers);
            var boundOptions = serviceProvider.GetRequiredService<IdentityLockoutOptions>();
            Assert.Equal(7, boundOptions.MaxFailedAccessAttempts);
            Assert.Equal(30, boundOptions.LockoutMinutes);
        }

        [Fact]
        public void AddInfrastructure_UsesTheRepositoryEmailDecisionAsTheIdentityNormalizer()
        {
            using var serviceProvider = BuildServiceProvider(CreateValidSettings());

            var normalizer = Assert.IsType<PlatformAdminLookupNormalizer>(serviceProvider.GetRequiredService<ILookupNormalizer>());

            Assert.Equal("active.admin@cafehub.app", normalizer.NormalizeEmail("  Active.Admin@CafeHub.App "));
        }

        [Fact]
        public void AddInfrastructure_RegistersTheSharedAdminAuthenticator()
        {
            using var serviceProvider = BuildServiceProvider(CreateValidSettings());

            Assert.IsType<AdminAuthenticator>(serviceProvider.GetRequiredService<IAdminAuthenticator>());
        }
    }
}
