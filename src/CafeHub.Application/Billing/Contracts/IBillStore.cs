using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Persistence port for the per-table settlement bill. The resolve finds the table's open bill or
    /// creates a tracked new one; the bill insert, its audit entry, the attachment, and the order
    /// decision persist together with the caller's next SaveChanges in one transaction.
    /// </summary>
    public interface IBillStore
    {
        Task<Result<Bill>> ResolveOpenBillAsync(
            Guid branchId,
            Guid tableId,
            DateTimeOffset now,
            Guid? actorId,
            CancellationToken cancellationToken);

        Task<BillSettlement?> FindForCloseAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken);

        Task<Result> SaveCloseAsync(
            Bill bill,
            decimal remainingAmount,
            Guid actorId,
            CancellationToken cancellationToken);
    }
}
