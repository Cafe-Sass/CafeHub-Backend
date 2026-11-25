namespace CafeHub.Domain.Tenants
{
    public sealed record StockAllocationLine(Guid BatchId, decimal Quantity);

    public static class StockAllocation
    {
        public static bool TryAllocate(
            IReadOnlyList<InventoryBatch> batches,
            decimal quantity,
            DateOnly today,
            bool allowExpired,
            out IReadOnlyList<StockAllocationLine> allocations)
        {
            ArgumentNullException.ThrowIfNull(batches);

            allocations = Array.Empty<StockAllocationLine>();
            if (quantity <= 0m
                || quantity > InventoryBatch.MaximumQuantity
                || quantity != decimal.Round(quantity, InventoryBatch.MaximumQuantityScale))
            {
                return false;
            }

            var candidates = batches
                .Where(batch => batch.RemainingQuantity > 0m)
                .Where(batch => allowExpired
                    || batch.ExpiryDate is null
                    || batch.ExpiryDate >= today)
                .OrderBy(batch => batch.ExpiryDate ?? DateOnly.MaxValue)
                .ThenBy(batch => batch.ReceivedDate)
                .ThenBy(batch => batch.Id)
                .ToList();

            var lines = new List<StockAllocationLine>(candidates.Count);
            var remaining = quantity;
            foreach (var batch in candidates)
            {
                var take = Math.Min(batch.RemainingQuantity, remaining);
                if (take <= 0m)
                {
                    continue;
                }

                lines.Add(new StockAllocationLine(batch.Id, take));
                remaining -= take;
                if (remaining == 0m)
                {
                    break;
                }
            }

            if (remaining != 0m)
            {
                return false;
            }

            allocations = lines;
            return true;
        }
    }
}
