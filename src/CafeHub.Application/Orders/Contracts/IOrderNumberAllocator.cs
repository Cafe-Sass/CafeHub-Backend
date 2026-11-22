namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Hands out the next order number for one branch on the current cafe-local business date. The counter is
    /// per branch and per business date, so a new day starts again at 1 and two branches never share numbers.
    /// The allocation also returns the business date it used, so the order row and the counter always agree.
    /// </summary>
    public interface IOrderNumberAllocator
    {
        Task<OrderNumberAllocation> AllocateAsync(Guid branchId, CancellationToken cancellationToken);
    }
}
