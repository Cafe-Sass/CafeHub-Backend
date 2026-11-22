using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Application.Orders
{
    public sealed class OrderNumberAllocator(
        IOrderNumberCounterStore counterStore, ICafeBusinessDateProvider businessDateProvider)
        : IOrderNumberAllocator
    {
        public async Task<OrderNumberAllocation> AllocateAsync(Guid branchId, CancellationToken cancellationToken)
        {
            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            var businessDate = await businessDateProvider.GetBusinessDateAsync(cancellationToken);
            var number = await counterStore.IncrementAsync(branchId, businessDate, cancellationToken);
            return new OrderNumberAllocation(number, businessDate);
        }
    }
}
