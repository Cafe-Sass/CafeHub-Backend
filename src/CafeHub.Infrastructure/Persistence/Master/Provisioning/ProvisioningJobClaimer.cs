using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.Infrastructure.Persistence.Master.Provisioning
{
    internal sealed class ProvisioningJobClaimer(MasterDbContext context) : IProvisioningJobClaimer
    {
        public async Task<Result<ClaimedProvisioningJob?>> TryClaimNextAsync(
            string workerId,
            DateTimeOffset now,
            TimeSpan leaseDuration,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(workerId))
            {
                return Result.Failure<ClaimedProvisioningJob?>(ProvisioningErrors.InvalidWorkerId());
            }

            if (leaseDuration <= TimeSpan.Zero)
            {
                return Result.Failure<ClaimedProvisioningJob?>(ProvisioningErrors.InvalidLeaseDuration());
            }

            var leaseExpiry = now.Add(leaseDuration);

            var sql = """
                UPDATE provisioning_jobs
                SET status = 'Running',
                    lease_expires_at = @leaseExpiry,
                    leased_by = @workerId,
                    updated_at = @now
                WHERE id = (
                    SELECT id FROM provisioning_jobs
                    WHERE (status = 'Pending'
                           OR (status = 'Running' AND lease_expires_at <= @now))
                      AND (due_at IS NULL OR due_at <= @now)
                    ORDER BY created_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING id AS "Id", tenant_id AS "TenantId"
                """;

            var claimedRows = await context.Database
                .SqlQueryRaw<ClaimedJobRow>(
                    sql,
                    new NpgsqlParameter("leaseExpiry", leaseExpiry),
                    new NpgsqlParameter("workerId", workerId),
                    new NpgsqlParameter("now", now))
                .ToListAsync(cancellationToken);

            // The UPDATE returns at most one row because the selected ID is limited to one job.
            // Materialize the non-composable UPDATE ... RETURNING SQL directly, then select locally.
            var claimedRow = claimedRows.FirstOrDefault();

            if (claimedRow is null)
            {
                return Result.Success<ClaimedProvisioningJob?>(null);
            }

            return Result.Success<ClaimedProvisioningJob?>(
                new ClaimedProvisioningJob(claimedRow.Id, claimedRow.TenantId));
        }

        private sealed record ClaimedJobRow(Guid Id, Guid TenantId);
    }
}

