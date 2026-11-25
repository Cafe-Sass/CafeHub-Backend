using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Contracts
{
    public sealed record PrintRouteTarget(Guid PrinterId, Guid? StationId, int LineWidth);

    /// <summary>
    /// Persistence port for the print queue. Queries are reads on the shared tenant context; <see cref="Add"/>
    /// only tracks a job so the caller's unit of work commits it with the business event that created it.
    /// </summary>
    public interface IPrintJobStore
    {
        Task<IReadOnlyList<PrintRouteTarget>> FindStationRoutesAsync(
            Guid branchId,
            PrintDocumentType documentType,
            IReadOnlyCollection<Guid> stationIds,
            CancellationToken cancellationToken);

        Task<PrintRouteTarget?> FindBranchRouteAsync(
            Guid branchId,
            PrintDocumentType documentType,
            CancellationToken cancellationToken);

        Task<IReadOnlySet<string>> FindExistingDedupeKeysAsync(
            Guid branchId,
            IReadOnlyCollection<string> dedupeKeys,
            CancellationToken cancellationToken);

        void Add(PrintJob job);
    }
}
