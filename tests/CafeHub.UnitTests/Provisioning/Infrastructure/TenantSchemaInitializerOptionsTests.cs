using CafeHub.Infrastructure.Provisioning;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class TenantSchemaInitializerOptionsTests
    {
        private static IConfiguration CreateConfiguration(string? adminConnectionString)
        {
            var settings = new Dictionary<string, string?>
            {
                [$"{TenantSchemaInitializerConfigurationKeys.SectionName}:{TenantSchemaInitializerConfigurationKeys.AdminConnectionStringKey}"] = adminConnectionString,
            };

            return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        }

        [Fact]
        public void Bind_WithValidConnectionString_ReturnsTheBoundOptions()
        {
            var configuration = CreateConfiguration("Host=localhost;Database=postgres");

            var options = TenantSchemaInitializerOptionsBinder.Bind(configuration);

            Assert.Equal("Host=localhost;Database=postgres", options.AdminConnectionString);
        }

        [Fact]
        public void Bind_WithAnEmptyAdminConnectionString_FailsWithoutEchoingTheValue()
        {
            var configuration = CreateConfiguration("");

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantSchemaInitializerOptionsBinder.Bind(configuration));

            Assert.Contains("AdminConnectionString", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Bind_WithAMissingSection_Throws()
        {
            var configuration = new ConfigurationBuilder().Build();

            var exception = Assert.Throws<InvalidOperationException>(
                () => TenantSchemaInitializerOptionsBinder.Bind(configuration));

            Assert.Contains("AdminConnectionString", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Validate_ReportsMissingConnectionString_WithoutEchoingTheValue()
        {
            var options = new TenantSchemaInitializerOptions();

            var problems = options.Validate();

            Assert.Contains(problems, problem => problem.Contains("AdminConnectionString", StringComparison.Ordinal));
            Assert.DoesNotContain(problems, problem => problem.Contains("Host=", StringComparison.Ordinal));
        }
    }
}
