using CafeHub.Infrastructure.Identity;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests
{
    public sealed class PlatformSeedOptionsBinderTests
    {
        private const string SeedEmail = "seed.superadmin@cafehub.app";
        private const string SeedPassword = "Str0ng!SeedPassword";

        private static IConfiguration CreateConfiguration(Dictionary<string, string?> settings)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        [Fact]
        public void Bind_WithConfiguredValues_ReturnsThemAndReportsConfigured()
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [PlatformSeedConfigurationKeys.SuperAdminEmailPath] = SeedEmail,
                [PlatformSeedConfigurationKeys.SuperAdminPasswordPath] = SeedPassword,
            });

            var options = PlatformSeedOptionsBinder.Bind(configuration);

            Assert.Equal(SeedEmail, options.SuperAdminEmail);
            Assert.Equal(SeedPassword, options.SuperAdminPassword);
            Assert.True(options.IsConfigured);
        }

        [Fact]
        public void Bind_WithoutTheSection_ReportsNotConfigured()
        {
            var options = PlatformSeedOptionsBinder.Bind(new ConfigurationBuilder().Build());

            Assert.Null(options.SuperAdminEmail);
            Assert.Null(options.SuperAdminPassword);
            Assert.False(options.IsConfigured);
        }

        [Fact]
        public void Bind_WithNullConfiguration_ReportsNotConfigured()
        {
            var options = PlatformSeedOptionsBinder.Bind(null);

            Assert.Null(options.SuperAdminEmail);
            Assert.Null(options.SuperAdminPassword);
            Assert.False(options.IsConfigured);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Bind_WithAnEmptyEmail_ReportsNotConfigured(string email)
        {
            var configuration = CreateConfiguration(new Dictionary<string, string?>
            {
                [PlatformSeedConfigurationKeys.SuperAdminEmailPath] = email,
                [PlatformSeedConfigurationKeys.SuperAdminPasswordPath] = SeedPassword,
            });

            var options = PlatformSeedOptionsBinder.Bind(configuration);

            Assert.False(options.IsConfigured);
        }
    }
}
