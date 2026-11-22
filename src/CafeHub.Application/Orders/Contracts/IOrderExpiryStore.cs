namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Write-side port for lazy expiry. Expires pending orders at or past their response deadline, scoped to
    /// one branch (the feed path) or one order (the decision path), and records the missed-response rows with
    /// the branch staff holding orders.decide at that moment. Returns the expired order ids; an empty result
    /// means nothing was stale and nothing was written.
    /// </summary>
    public interface IOrderExpiryStore
    {
        Task<IReadOnlyList<Guid>> ExpireStaleOrdersAsync(
            Guid? branchId,
            Guid? orderId,
            DateTimeOffset now,
            CancellationToken cancellationToken);
    }
}
