using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Inventory
{
    public static class StockMovementReplay
    {
        public static bool Matches(
            IReadOnlyList<StockMovement> existing,
            Guid itemId,
            StockMovementType type,
            string reason,
            decimal quantity,
            Guid? batchId)
        {
            ArgumentNullException.ThrowIfNull(existing);

            if (existing.Count == 0
                || existing.Any(movement => movement.ItemId != itemId
                    || movement.Type != type
                    || !string.Equals(movement.Reason, reason, StringComparison.Ordinal)))
            {
                return false;
            }

            if (type == StockMovementType.Adjust)
            {
                return existing.Count == 1
                    && existing[0].BatchId == batchId
                    && existing[0].Quantity == quantity;
            }

            return batchId is null && existing.Sum(movement => -movement.Quantity) == quantity;
        }
    }
}
