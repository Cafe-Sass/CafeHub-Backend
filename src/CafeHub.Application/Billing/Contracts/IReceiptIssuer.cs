using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Billing.Contracts
{
    /// <summary>
    /// Issues the immutable digital receipt when a bill reaches zero remaining with nothing pending.
    /// The bill row is already locked and closed by the caller, the call runs inside the caller's
    /// transaction, and a repeat for the same bill returns success without a second receipt.
    /// </summary>
    public interface IReceiptIssuer
    {
        Task<Result> IssueAsync(
            Bill bill,
            IReadOnlyList<Order> acceptedOrders,
            IReadOnlyList<Payment> payments,
            Guid actorId,
            DateTimeOffset now,
            CancellationToken cancellationToken);
    }
}
