using System.Collections.Generic;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Tables.Contracts
{
    /// <summary>
    /// Persistence port for a tenant's branch tables. The tenant is resolved from the write-once tenant
    /// context; the store never accepts a tenant identifier from callers. <c>scopeBranchId</c> carries the
    /// branch scope of a delegated staff user and is null for the Owner. Each write commits the table
    /// change and one tenant audit entry together in a single tenant transaction.
    /// </summary>
    public interface ITableStore
    {
        Task<Result<TableResponse>> AddAsync(
            Guid branchId,
            string? label,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<TableBulkResult>> AddBulkAsync(
            Guid branchId,
            int count,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<TableResponse>> UpdateLabelAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string? label,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<TableResponse>> DeactivateAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<TableResponse>> ReactivateAsync(
            Guid tableId,
            Guid? scopeBranchId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<TableBulkResult>> DeactivateHighestAsync(
            Guid branchId,
            int count,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<TableResponse?> GetByIdAsync(
            Guid tableId,
            Guid? scopeBranchId,
            CancellationToken cancellationToken);

        Task<TableListPage> ListAsync(TableListRequest request, CancellationToken cancellationToken);

        Task<TableQrData?> GetQrAsync(Guid branchId, Guid tableId, CancellationToken cancellationToken);

        Task<IReadOnlyList<TableQrData>?> GetQrSheetAsync(Guid branchId, CancellationToken cancellationToken);

        Task<Result<TableQrData>> RegenerateQrAsync(
            Guid branchId,
            Guid tableId,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<GuestQrContext?> ResolveGuestQrAsync(string code, CancellationToken cancellationToken);

        Task<int> CountActiveAsync(Guid branchId, CancellationToken cancellationToken);

        Task<int?> MaxActiveNumberAsync(Guid branchId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Offset-page request for one branch's table list, with optional status and number-range filters.
    /// <see cref="PageSize"/> is the resolved page size; the store fetches at most one extra row so the
    /// caller can derive <c>HasNextPage</c> without a count.
    /// </summary>
    public sealed record TableListRequest(
        Guid BranchId,
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        BranchTableStatus? Status,
        int? FromNumber,
        int? ToNumber);

    /// <summary>
    /// One tables list page as read from persistence. <see cref="Items"/> contains at most the requested
    /// page size plus one row; the extra row signals a following page and is trimmed by the query handler.
    /// <see cref="TotalCount"/> is populated only when the request asked for it.
    /// </summary>
    public sealed record TableListPage(IReadOnlyList<TableResponse> Items, int? TotalCount);
}
