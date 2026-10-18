using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.UnitTests
{
    public sealed class TenantDbContextFactoryTests
    {
        [Fact]
        public void CreateDbContext_Throws_WhenDesignTimeEnvironmentVariableMissing()
        {
            Environment.SetEnvironmentVariable(ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable, null);

            try
            {
                var factory = new TenantDbContextFactory();

                var exception = Record.Exception(() => factory.CreateDbContext([]));

                Assert.IsType<InvalidOperationException>(exception);
                Assert.Contains(ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable, exception.Message);
                Assert.DoesNotContain("=", exception.Message, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable, null);
            }
        }

        [Fact]
        public void CreateDbContext_UsesConfiguredDesignTimeConnection()
        {
            const string designTimeConnection = "Host=localhost;Port=5432;Database=cafehub_tenant_design_time";

            try
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable, designTimeConnection);
                var context = new TenantDbContextFactory().CreateDbContext([]);

                Assert.Equal(designTimeConnection, context.Database.GetConnectionString());
            }
            finally
            {
                Environment.SetEnvironmentVariable(ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable, null);
            }
        }
    }
}
