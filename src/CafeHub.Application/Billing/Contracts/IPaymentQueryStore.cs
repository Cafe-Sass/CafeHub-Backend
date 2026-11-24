using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Read port for the staff payment review. The list pages one branch's payments newest first and
    /// exposes only review data; the proof lookup resolves a payment only inside the caller's branch
    /// scope, so a foreign payment is indistinguishable from a missing one. Both reads are lock-free;
    /// the review write keeps its own locking snapshot.
    /// </summary>
    public interface IPaymentQueryStore
    {
        Task<PaymentListPage> ListAsync(PaymentListRequest request, CancellationToken cancellationToken);

        Task<Payment?> FindProofAsync(
            Guid? branchId,
            Guid paymentId,
            CancellationToken cancellationToken);
    }
}
