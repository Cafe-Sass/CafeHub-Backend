using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Application.Orders
{
    public sealed class OrderNumberAllocator(
        IOrderNumberCounterStore counterStore, ICafeBusinessDateProvider businessDateProvider)
        : IOrderNumberAllocator
    {
        public async Task<int> AllocateAsync(Guid branchId, CancellationToken cancellationToken)
        {
            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            var businessDate = await businessDateProvider.GetBusinessDateAsync(cancellationToken);
            return await counterStore.IncrementAsync(branchId, businessDate, cancellationToken);
        }
    }
}
