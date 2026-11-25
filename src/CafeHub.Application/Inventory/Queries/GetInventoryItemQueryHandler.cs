using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;

namespace CafeHub.Application.Inventory.Queries
{
    public sealed class GetInventoryItemQueryHandler(IInventoryItemStore store)
        : IQueryHandler<GetInventoryItemQuery, InventoryItemResponse>
    {
        public async Task<Result<InventoryItemResponse>> Handle(
            GetInventoryItemQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var item = await store.GetByIdAsync(query.Id, cancellationToken);
            return item is null
                ? Result.Failure<InventoryItemResponse>(InventoryItemErrors.NotFound())
                : Result.Success(item);
        }
    }
}
