using CafeHub.Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class TenantDatabaseCreatorOptionsTests
    {
        private const string ShortBase64Key = "c2hvcnQ=";
        private static readonly string ValidMasterKey = Convert.ToBase64String(new byte[32]);

        private static IConfiguration CreateConfiguration(string? adminConnectionString, string? credentialMasterKey)
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{TenantDatabaseCreatorConfigurationKeys.SectionName}:{TenantDatabaseCreatorConfigurationKeys.AdminConnectionStringKey}"] = adminConnectionString,
                [$"{TenantDatabaseCreatorConfigurationKeys.SectionName}:{TenantDatabaseCreatorConfigurationKeys.CredentialMasterKeyKey}"] = credentialMasterKey,
            };

            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        [Fact]
        public void Bind_WithValidValues_ReturnsTheBoundOptions()
        {
            var configuration = CreateConfiguration("Host=localhost;Database=postgres", ValidMasterKey);

            var options = TenantDatabaseCreatorOptionsBinder.Bind(configuration);

            Assert.Equal("Host=localhost;Database=postgres", options.AdminConnectionString);
            Assert.Equal(ValidMasterKey, options.CredentialMasterKey);
        }

        [Fact]
        public void Bind_WithAnEmptyAdminConnectionString_FailsWithoutEchoingTheValue()
        {
            var configuration = CreateConfiguration("", ValidMasterKey);

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseCreatorOptionsBinder.Bind(configuration));

            Assert.Contains("AdminConnectionString", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithAnEmptyMasterKey_FailsWithoutEchoingTheValue()
        {
            var configuration = CreateConfiguration("Host=localhost;Database=postgres", "");

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseCreatorOptionsBinder.Bind(configuration));

            Assert.Contains("CredentialMasterKey", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithAMasterKeyShorterThanThirtyTwoBytes_FailsWithoutEchoingTheValue()
        {
            var configuration = CreateConfiguration("Host=localhost;Database=postgres", ShortBase64Key);

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseCreatorOptionsBinder.Bind(configuration));

            Assert.Contains("CredentialMasterKey", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(ShortBase64Key, exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithANonBase64MasterKey_FailsWithoutEchoingTheValue()
        {
            var configuration = CreateConfiguration("Host=localhost;Database=postgres", "not-base64!!");

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantDatabaseCreatorOptionsBinder.Bind(configuration));

            Assert.Contains("CredentialMasterKey", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("not-base64!!", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_ReportsEveryMissingSecret_WithoutEchoingAnyValue()
        {
            var options = new TenantDatabaseCreatorOptions();

            var problems = options.Validate();

            Assert.Contains(problems, problem => problem.Contains("AdminConnectionString", StringComparison.Ordinal));
            Assert.Contains(problems, problem => problem.Contains("CredentialMasterKey", StringComparison.Ordinal));
        }
    }
}
