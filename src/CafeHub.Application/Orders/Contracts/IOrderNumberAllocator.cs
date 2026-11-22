namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Hands out the next order number for one branch on the current cafe-local business date. The counter is
    /// per branch and per business date, so a new day starts again at 1 and two branches never share numbers.
    /// </summary>
    public interface IOrderNumberAllocator
    {
        Task<int> AllocateAsync(Guid branchId, CancellationToken cancellationToken);
    }
}
