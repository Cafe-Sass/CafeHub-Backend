using CafeHub.Application.Printing.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Printing
{
    internal sealed class PrintJobStore(TenantDbContext context) : IPrintJobStore
    {
        private const int FallbackLineWidth = 48;

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

        private static int LineWidth(PrinterProfile profile) =>
            PrinterProfiles.TryGet(profile, out var definition) && definition is not null
                ? definition.LineWidth
                : FallbackLineWidth;
    }
}
