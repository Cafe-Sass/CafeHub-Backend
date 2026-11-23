using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Persistence port for payments. The settlement snapshot is always loaded inside the caller's
    /// branch scope and locks the bill row, so two concurrent confirmations cannot both pass the
    /// remaining check; the add persists the payment and its audit record as one save, and a concurrent
    /// confirm that lost the (bill, idempotency key) race returns the stored payment instead of a
    /// database error.
    /// </summary>
    public interface IPaymentStore
    {
        Task<BillSettlement?> FindSettlementAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken);

        Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken);
    }
}
