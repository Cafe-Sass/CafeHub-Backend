using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Applies the Tenant migration to a named tenant database using the administrative connection.
    /// Only the database name is swapped into the admin connection string; nothing is logged.
    /// </summary>
    internal sealed class NpgsqlTenantMigrationApplicator : ITenantMigrationApplicator
    {
        private readonly TenantSchemaInitializerOptions _options;

        public NpgsqlTenantMigrationApplicator(TenantSchemaInitializerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task ApplyAsync(string databaseName, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("The database name is required.", nameof(databaseName));
            }

            var connectionString = new NpgsqlConnectionStringBuilder(_options.AdminConnectionString)
            {
                Database = databaseName,
            }.ConnectionString;

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            using var context = new TenantDbContext(options);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
