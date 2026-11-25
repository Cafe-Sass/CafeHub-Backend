using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Contracts
{
    /// <summary>
    /// Persistence port for the per-branch printer registry and its document routes. Reads and writes
    /// stay inside the supplied branch; the store rejects a station or printer from another branch, and
    /// each write commits its change and one tenant audit entry together. Routes are configuration, so a
    /// set either creates the primary route for a branch/station/document or changes its printer.
    /// </summary>
    public interface IPrinterStore
    {
        Task<Result<PrinterResponse>> RegisterAsync(
            Guid branchId,
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<PrinterResponse>> UpdateAsync(
            Guid printerId,
            Guid branchId,
            Guid? stationId,
            string name,
            PrinterProfile profile,
            string connectionKind,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<PrinterResponse>> SetStatusAsync(
            Guid printerId,
            Guid branchId,
            PrinterStatus status,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<PrinterResponse?> FindPrinterAsync(
            Guid printerId,
            Guid branchId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<PrinterResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken);

        Task<Result<PrintRouteResponse>> SetRouteAsync(
            Guid branchId,
            Guid? stationId,
            PrintDocumentType documentType,
            Guid printerId,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<PrintRouteResponse>> ListRoutesAsync(
            Guid branchId,
            CancellationToken cancellationToken);
    }
}
