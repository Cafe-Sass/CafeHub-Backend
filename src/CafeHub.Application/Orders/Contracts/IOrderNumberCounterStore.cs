namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Increments the branch counter for one business date atomically and returns the new value. Implementations
    /// must serialize concurrent callers on the counter row so no two committed orders receive the same number.
    /// </summary>
    public interface IOrderNumberCounterStore
    {
        Task<int> IncrementAsync(Guid branchId, DateOnly businessDate, CancellationToken cancellationToken);
    }
}
