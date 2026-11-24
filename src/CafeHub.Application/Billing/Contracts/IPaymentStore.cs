using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Persistence port for payments. The settlement snapshot is always loaded inside the caller's
    /// branch scope and locks the bill row, so two concurrent confirmations cannot both pass the
    /// remaining check; the add persists the payment and its audit record as one save, and a concurrent
    /// confirm that lost the (bill, idempotency key) race returns the stored payment instead of a
    /// database error. A guest-submitted proof is persisted without an audit record: the submission
    /// is anonymous and the proof note, media key, and image are never audited. A staff decision loads
    /// the locked settlement and persists the payment with its audit record as one save.
    /// </summary>
    public interface IPaymentStore
    {
        Task<BillSettlement?> FindSettlementAsync(
            Guid? branchId,
            Guid billId,
            CancellationToken cancellationToken);

        Task<BillSettlement?> FindForProofAsync(
            string billReference,
            CancellationToken cancellationToken);

        Task<PaymentReview?> FindReviewAsync(
            Guid? branchId,
            Guid paymentId,
            CancellationToken cancellationToken);

        Task<Result<Payment>> AddProofAsync(Payment payment, CancellationToken cancellationToken);

        Task<Result<Payment>> SaveReviewAsync(
            Payment payment,
            string? rejectionReason,
            CancellationToken cancellationToken);

        Task<Result<Payment>> AddAsync(Payment payment, CancellationToken cancellationToken);
    }
}
