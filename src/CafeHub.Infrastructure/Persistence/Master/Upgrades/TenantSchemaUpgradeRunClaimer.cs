using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Upgrades;
using CafeHub.Application.Upgrades.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Persistence.Master.Upgrades
{
    internal sealed class TenantSchemaUpgradeRunClaimer(MasterDbContext context) : ITenantSchemaUpgradeRunClaimer
    {
        public async Task<Result<ClaimedTenantSchemaUpgradeRun?>> TryClaimNextAsync(
            string workerId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workerId))
            {
                return Result.Failure<ClaimedTenantSchemaUpgradeRun?>(TenantSchemaUpgradeErrors.InvalidWorkerId());
            }

            if (leaseDuration <= TimeSpan.Zero)
            {
                return Result.Failure<ClaimedTenantSchemaUpgradeRun?>(TenantSchemaUpgradeErrors.InvalidLeaseDuration());
            }

            var leaseExpiry = now.Add(leaseDuration);

            var sql = """
                UPDATE tenant_schema_upgrade_runs
                SET status = 'Running',
                    attempt_count = attempt_count + 1,
                    started_at = COALESCE(started_at, @now),
                    lease_expires_at = @leaseExpiry,
                    leased_by = @workerId,
                    updated_at = @now
                WHERE id = (
                    SELECT id FROM tenant_schema_upgrade_runs
                    WHERE status = 'Pending'
                       OR (status = 'Running' AND lease_expires_at <= @now)
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING id AS "RunId", tenant_id AS "TenantId"
                """;

            var claimedRows = await context.Database
                .SqlQueryRaw<ClaimedRunRow>(
                    sql,
                    new NpgsqlParameter("leaseExpiry", leaseExpiry),
                    new NpgsqlParameter("workerId", workerId),
                    new NpgsqlParameter("now", now))
                .ToListAsync(cancellationToken);

            var claimedRow = claimedRows.FirstOrDefault();

            if (claimedRow is null)
            {
                return Result.Success<ClaimedTenantSchemaUpgradeRun?>(null);
            }

            return Result.Success<ClaimedTenantSchemaUpgradeRun?>(
                new ClaimedTenantSchemaUpgradeRun(claimedRow.RunId, claimedRow.TenantId));
        }

        private sealed record ClaimedRunRow(Guid RunId, Guid TenantId);
    }
}
