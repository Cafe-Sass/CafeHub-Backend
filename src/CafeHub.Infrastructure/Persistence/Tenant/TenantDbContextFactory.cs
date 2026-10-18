using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CafeHub.Infrastructure.Persistence.Tenant
{
    public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
    {
        public TenantDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Tenant design-time tooling requires the environment variable \"{ConfigurationKeys.TenantDesignTimeConnectionStringEnvironmentVariable}\" set to a local developer tenant connection string. It is never used at runtime.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<TenantDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new TenantDbContext(optionsBuilder.Options);
        }
    }
}
