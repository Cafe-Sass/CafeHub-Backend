using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Printing
{
    internal sealed class PrintJobStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IPrintJobStore, IPrintJobOperationsStore
    {
        private const int FallbackLineWidth = 48;
        private const int ExpiredLeaseBatchSize = 50;
        private const string TargetType = "print_jobs";
        private const string ClaimSql =
            "SELECT * FROM print_jobs WHERE branch_id = {0} AND status = 'Queued' " +
            "ORDER BY created_at, id FOR UPDATE SKIP LOCKED LIMIT 1";

        public async Task<IReadOnlyList<PrintRouteTarget>> FindStationRoutesAsync(
            Guid branchId,
            PrintDocumentType documentType,
            IReadOnlyCollection<Guid> stationIds,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stationIds);
            if (stationIds.Count == 0)
            {
                return [];
            }

            var rows = await (from route in context.PrintRoutes.AsNoTracking()
                              join printer in context.Printers.AsNoTracking()
                                  on route.PrinterId equals printer.Id
                              where route.BranchId == branchId
                                  && route.DocumentType == documentType
                                  && route.StationId != null
                                  && stationIds.Contains(route.StationId.Value)
                                  && printer.Status == PrinterStatus.Active
                              select new { route.StationId, route.PrinterId, printer.Profile })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new PrintRouteTarget(row.PrinterId, row.StationId, LineWidth(row.Profile)))
                .ToArray();
        }

        public async Task<PrintRouteTarget?> FindBranchRouteAsync(
            Guid branchId,
            PrintDocumentType documentType,
            CancellationToken cancellationToken)
        {
            var row = await (from route in context.PrintRoutes.AsNoTracking()
                             join printer in context.Printers.AsNoTracking()
                                 on route.PrinterId equals printer.Id
                             where route.BranchId == branchId
                                 && route.DocumentType == documentType
                                 && route.StationId == null
                                 && printer.Status == PrinterStatus.Active
                             select new { route.StationId, route.PrinterId, printer.Profile })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null
                ? null
                : new PrintRouteTarget(row.PrinterId, row.StationId, LineWidth(row.Profile));
        }

        public async Task<IReadOnlySet<string>> FindExistingDedupeKeysAsync(
            Guid branchId,
            IReadOnlyCollection<string> dedupeKeys,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(dedupeKeys);
            if (dedupeKeys.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            var keys = await context.PrintJobs.AsNoTracking()
                .Where(job => job.BranchId == branchId && dedupeKeys.Contains(job.DedupeKey))
                .Select(job => job.DedupeKey)
                .ToListAsync(cancellationToken);
            return new HashSet<string>(keys, StringComparer.Ordinal);
        }

        public void Add(PrintJob job) => context.PrintJobs.Add(job);

        public async Task<PrintJob?> ClaimNextAsync(
            Guid branchId,
            TimeSpan lease,
            CancellationToken cancellationToken)
        {
            var now = timeProvider.GetUtcNow();
            var expired = await context.PrintJobs
                .Where(job => job.BranchId == branchId
                    && job.Status == PrintJobStatus.Claimed
                    && job.LeaseExpiresAt != null
                    && job.LeaseExpiresAt <= now)
                .OrderBy(job => job.LeaseExpiresAt)
                .Take(ExpiredLeaseBatchSize)
                .ToListAsync(cancellationToken);
            foreach (var job in expired)
            {
                job.RequeueExpiredLease(now);
            }

            if (context.ChangeTracker.HasChanges())
            {
                await context.SaveChangesAsync(cancellationToken);
            }

            var candidates = await context.PrintJobs
                .FromSqlRaw(ClaimSql, branchId)
                .ToListAsync(cancellationToken);
            var candidate = candidates.SingleOrDefault();
            if (candidate is null || !candidate.TryClaim(now, lease))
            {
                return null;
            }

            await context.SaveChangesAsync(cancellationToken);
            return candidate;
        }

        public async Task<Result<PrintJobResponse>> ReportSucceededAsync(
            Guid jobId,
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var job = await LoadJobAsync(jobId, branchId, cancellationToken);
            if (job is null)
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotFound());
            }

            if (job.Status == PrintJobStatus.Succeeded)
            {
                return Result.Success(PrintJobResponse.From(job));
            }

            if (!job.ReportSucceeded(timeProvider.GetUtcNow()))
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotReportable());
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintJobResponse.From(job));
        }

        public async Task<Result<PrintJobResponse>> ReportFailedAsync(
            Guid jobId,
            Guid branchId,
            string failureCode,
            CancellationToken cancellationToken)
        {
            if (!PrintFailureCodes.IsSupported(failureCode))
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.InvalidFailureCode());
            }

            var job = await LoadJobAsync(jobId, branchId, cancellationToken);
            if (job is null)
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotFound());
            }

            if (!job.ReportFailed(failureCode, timeProvider.GetUtcNow()))
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotReportable());
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintJobResponse.From(job));
        }

        public async Task<Result<PrintJobResponse>> CancelAsync(
            Guid jobId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var job = await LoadJobAsync(jobId, branchId, cancellationToken);
            if (job is null)
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotFound());
            }

            if (job.Status == PrintJobStatus.Succeeded)
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotCancellable());
            }

            var previousStatus = job.Status;
            if (!job.Cancel(timeProvider.GetUtcNow()))
            {
                return Result.Success(PrintJobResponse.From(job));
            }

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "print_jobs.cancelled.succeeded",
                TargetType = TargetType,
                TargetId = job.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintJob.Status), previousStatus.ToString(), job.Status.ToString()),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<PrintJobResponse>(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintJobResponse.From(job));
        }

        public async Task<Result<PrintJobResponse>> RequeueAsync(
            Guid jobId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var job = await LoadJobAsync(jobId, branchId, cancellationToken);
            if (job is null)
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotFound());
            }

            var previousStatus = job.Status;
            var previousAttempts = job.Attempts;
            var previousError = job.LastErrorCode;
            if (!job.Requeue(timeProvider.GetUtcNow()))
            {
                return Result.Failure<PrintJobResponse>(PrintingErrors.JobNotRequeuable());
            }

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "print_jobs.requeued.succeeded",
                TargetType = TargetType,
                TargetId = job.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintJob.Status), previousStatus.ToString(), job.Status.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintJob.Attempts),
                        previousAttempts.ToString(),
                        job.Attempts.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintJob.LastErrorCode), previousError, job.LastErrorCode),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<PrintJobResponse>(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintJobResponse.From(job));
        }

        public async Task<IReadOnlyList<PrintJobResponse>> ListAsync(
            Guid branchId,
            PrintJobStatus? status,
            Guid? printerId,
            PrintDocumentType? documentType,
            CancellationToken cancellationToken)
        {
            var queryable = context.PrintJobs.AsNoTracking()
                .Where(job => job.BranchId == branchId);
            if (status is not null)
            {
                queryable = queryable.Where(job => job.Status == status.Value);
            }

            if (printerId is not null)
            {
                queryable = queryable.Where(job => job.PrinterId == printerId.Value);
            }

            if (documentType is not null)
            {
                queryable = queryable.Where(job => job.DocumentType == documentType.Value);
            }

            var jobs = await queryable
                .OrderByDescending(job => job.CreatedAt)
                .ThenBy(job => job.Id)
                .ToListAsync(cancellationToken);
            return jobs.Select(PrintJobResponse.From).ToArray();
        }

        private async Task<PrintJob?> LoadJobAsync(
            Guid jobId,
            Guid branchId,
            CancellationToken cancellationToken) =>
            await context.PrintJobs.FirstOrDefaultAsync(
                job => job.Id == jobId && job.BranchId == branchId,
                cancellationToken);

        private static int LineWidth(PrinterProfile profile) =>
            PrinterProfiles.TryGet(profile, out var definition) && definition is not null
                ? definition.LineWidth
                : FallbackLineWidth;
    }
}
