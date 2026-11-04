using System.Data.Common;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Contracts;
using Npgsql;

namespace CafeHub.Infrastructure.Upgrades
{
    internal sealed class NpgsqlTenantSchemaUpgrader(ITenantMigrationApplicator migrationApplicator) : ITenantSchemaUpgrader
    {
        public async Task<Result<string?>> UpgradeAsync(
            string databaseName,
            CancellationToken cancellationToken)
        {
            try
            {
                await migrationApplicator.ApplyAsync(databaseName, cancellationToken);

                var appliedMigrationId = await migrationApplicator.GetLatestAppliedMigrationIdAsync(
                    databaseName,
                    cancellationToken);

                return Result.Success(appliedMigrationId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (PostgresException)
            {
                return Result.Failure<string?>(TenantSchemaUpgradeErrors.MigrationFailed());
            }
            catch (NpgsqlException)
            {
                return Result.Failure<string?>(TenantSchemaUpgradeErrors.ConnectionFailed());
            }
            catch (TimeoutException)
            {
                return Result.Failure<string?>(TenantSchemaUpgradeErrors.ConnectionFailed());
            }
            catch (DbException)
            {
                return Result.Failure<string?>(TenantSchemaUpgradeErrors.MigrationFailed());
            }
            catch (Exception)
            {
                return Result.Failure<string?>(TenantSchemaUpgradeErrors.Unexpected());
            }
        }
    }
}
