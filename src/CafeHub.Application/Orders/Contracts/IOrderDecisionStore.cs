using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Persistence port for accept and reject decisions. The target is always loaded inside the caller's branch
    /// scope (a null branch id is the owner path across branches); the decision is persisted as one update
    /// guarded by the row version, so a lost race returns Conflict with the current status. A pending bill
    /// insert that lost the one-open-bill race is resolved by re-reading the winning open bill and
    /// re-attaching the order, never surfaced as a database error.
    /// </summary>
    public interface IOrderDecisionStore
    {
        Task<Order?> FindDecisionTargetAsync(Guid? branchId, Guid orderId, CancellationToken cancellationToken);

        Task<Result<OrderStatus>> SaveDecisionAsync(Order order, CancellationToken cancellationToken);
    }
}
