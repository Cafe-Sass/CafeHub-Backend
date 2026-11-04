using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Applies and reads the Tenant migration state of a named tenant database using the administrative
    /// connection. Only the database name is swapped into the admin connection string; nothing is logged.
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

            using var context = CreateContext(databaseName);
            await context.Database.MigrateAsync(cancellationToken);

            var applicationRoleName = TenantDatabaseNaming.ApplicationRoleName(databaseName);
            var quotedRoleName = new NpgsqlCommandBuilder().QuoteIdentifier(applicationRoleName);
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"GRANT USAGE ON SCHEMA public TO {quotedRoleName}; " +
                $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {quotedRoleName}; " +
                $"GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO {quotedRoleName}; " +
                $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedRoleName}; " +
                $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE ON SEQUENCES TO {quotedRoleName}";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<string?> GetLatestAppliedMigrationIdAsync(
            string databaseName,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("The database name is required.", nameof(databaseName));
            }

            using var context = CreateContext(databaseName);
            var knownMigrations = context.Database.GetMigrations().ToList();
            var appliedMigrations = (await context.Database.GetAppliedMigrationsAsync(cancellationToken))
                .ToHashSet(StringComparer.Ordinal);

            return knownMigrations.LastOrDefault(appliedMigrations.Contains);
        }

        public Task<string?> GetLatestMigrationIdAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(_options.AdminConnectionString)
                .Options;

            using var context = new TenantDbContext(options);
            return Task.FromResult(context.Database.GetMigrations().LastOrDefault());
        }

        private TenantDbContext CreateContext(string databaseName)
        {
            var connectionString = new NpgsqlConnectionStringBuilder(_options.AdminConnectionString)
            {
                Database = databaseName,
            }.ConnectionString;

            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            return new TenantDbContext(options);
        }
    }
}
