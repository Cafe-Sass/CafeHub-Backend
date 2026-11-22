using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Persistence port for order submission. The branch, table, business date, and order number are never read
    /// from the request: they come from the resolved table QR and the allocator, so the store never accepts a
    /// tenant or table identifier from a caller.
    /// </summary>
    public interface IOrderStore
    {
        Task<OrderCatalog> LoadCatalogAsync(
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken);

        Task<Result<SubmitOrderResult>> AddAsync(Order order, CancellationToken cancellationToken);
    }
}
