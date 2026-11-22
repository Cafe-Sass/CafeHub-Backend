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
    }
}
