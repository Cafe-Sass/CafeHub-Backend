using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CafeHub.Infrastructure.Persistence.Master
{
    public sealed class MasterDbContextFactory : IDesignTimeDbContextFactory<MasterDbContext>
    {
        public MasterDbContext CreateDbContext(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable(ConfigurationKeys.MasterConnectionStringEnvironmentVariable)
                ?? throw new InvalidOperationException(
                    $"The master database design-time connection string is not configured. Set the environment variable \"{ConfigurationKeys.MasterConnectionStringEnvironmentVariable}\".");

            var optionsBuilder = new DbContextOptionsBuilder<MasterDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new MasterDbContext(optionsBuilder.Options);
        }
    }
}

