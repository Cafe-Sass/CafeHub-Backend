using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Read-side persistence port for the staff order feed. The branch and the visible station ids always come
    /// from the authenticated caller's scope, never from the client payload; the store fetches pageSize plus one
    /// rows so the handler can derive HasNextPage without a count.
    /// </summary>
    public interface IOrderFeedStore
    {
        Task<OrderFeedPage> ListFeedAsync(OrderFeedListRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// Finds one order with its items. A null branch id (the owner path) searches every branch; a staff
        /// caller always passes their own branch, so a foreign order is indistinguishable from a missing one.
        /// </summary>
        Task<OrderFeedEntry?> FindByIdAsync(Guid? branchId, Guid orderId, CancellationToken cancellationToken);
    }
}
