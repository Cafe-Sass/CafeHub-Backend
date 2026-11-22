using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Applies and reads the Tenant migration state of a named tenant database using the administrative
    /// connection. Only the database name is swapped into the admin connection string; nothing is logged.
    /// </summary>
    internal sealed class NpgsqlTenantMigrationApplicator : ITenantMigrationApplicator
    {
        private const string RoutineTargetMigrationId = "20261122200337_AddOrderResponseMisses";
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
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = (NpgsqlConnection)context.Database.GetDbConnection();
            var history = context.GetService<IHistoryRepository>();
            await history.CreateIfNotExistsAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await using var migrationLock = await history.AcquireDatabaseLockAsync(cancellationToken);
            var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            if (applied.Contains(TenantEmailBackfill.InitialTenantMigrationId) && !applied.Contains(TenantEmailBackfill.MigrationId))
            {
                await TenantEmailBackfill.PrepareAsync(connection, cancellationToken);
            }

            // An explicitly finalized tenant must never be downgraded by a routine run.
            var known = context.Database.GetMigrations().ToArray();
            var targetIndex = Array.IndexOf(known, RoutineTargetMigrationId);
            if (targetIndex < 0)
            {
                throw new InvalidOperationException("The routine Tenant migration target is missing.");
            }

            if (!known.Skip(targetIndex).Any(applied.Contains))
            {
                await context.GetService<IMigrator>().MigrateAsync(RoutineTargetMigrationId, cancellationToken);
            }

            var applicationRoleName = TenantDatabaseNaming.ApplicationRoleName(databaseName);
            var quotedRoleName = new NpgsqlCommandBuilder().QuoteIdentifier(applicationRoleName);
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"GRANT USAGE ON SCHEMA public TO {quotedRoleName}; " +
                $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {quotedRoleName}; " +
                $"GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO {quotedRoleName}; " +
                $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedRoleName}; " +
                $"ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE ON SEQUENCES TO {quotedRoleName}; " + TenantReadinessPrivileges.ReadOnlySql;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
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

            return Task.FromResult<string?>(RoutineTargetMigrationId);
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
