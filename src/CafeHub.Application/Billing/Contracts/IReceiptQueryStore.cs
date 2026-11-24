using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Read port for the immutable receipt snapshots. Every lookup is lock-free and scoped by the
    /// bill's branch when a branch id is supplied; the guest lookup resolves the bill by its public
    /// reference and only returns a receipt when the bill was completed, so an unpaid bill is
    /// indistinguishable from a missing one.
    /// </summary>
    public interface IReceiptQueryStore
    {
        Task<Receipt?> FindByIdAsync(
            Guid? branchId,
            Guid receiptId,
            CancellationToken cancellationToken);

        Task<Receipt?> FindByBillAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken);

        Task<Receipt?> FindByBillReferenceAsync(
            string billReference,
            CancellationToken cancellationToken);
    }
}
