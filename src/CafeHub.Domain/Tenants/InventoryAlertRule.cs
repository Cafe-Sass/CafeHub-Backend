namespace CafeHub.Domain.Tenants
{
    public sealed record InventoryAlertCondition(
        Guid ItemId,
        Guid BranchId,
        Guid? BatchId,
        InventoryAlertKind Kind);

    public sealed record InventoryAlertPlan(
        IReadOnlyList<InventoryAlertCondition> Raise,
        IReadOnlyList<InventoryAlert> Resolve);

    public static class InventoryAlertRule
    {
        public static bool IsLowStock(decimal remainingQuantity, decimal lowStockThreshold) =>
            remainingQuantity <= lowStockThreshold;

        public static InventoryAlertKind? ClassifyExpiry(
            DateOnly? expiryDate, DateOnly today, int nearExpiryDays)
        {
            if (expiryDate is not { } expiry)
            {
                return null;
            }

            if (expiry < today)
            {
                return InventoryAlertKind.Expired;
            }

            return expiry <= today.AddDays(nearExpiryDays)
                ? InventoryAlertKind.NearExpiry
                : null;
        }

        public static InventoryAlertPlan Plan(
            IEnumerable<InventoryAlert> openAlerts,
            IEnumerable<InventoryAlertCondition> conditions)
        {
            ArgumentNullException.ThrowIfNull(openAlerts);
            ArgumentNullException.ThrowIfNull(conditions);

            var desired = new HashSet<(Guid, Guid, Guid?, InventoryAlertKind)>();
            var raise = new List<InventoryAlertCondition>();
            foreach (var condition in conditions)
            {
                if (desired.Add(Key(condition)))
                {
                    raise.Add(condition);
                }
            }

            var open = openAlerts
                .Where(alert => alert.State == InventoryAlertState.Open)
                .ToList();
            var existing = open.Select(alert => Key(alert.ItemId, alert.BranchId, alert.BatchId, alert.Kind))
                .ToHashSet();

            return new InventoryAlertPlan(
                raise.Where(condition => !existing.Contains(Key(condition))).ToList(),
                open.Where(alert => !desired.Contains(
                        Key(alert.ItemId, alert.BranchId, alert.BatchId, alert.Kind)))
                    .ToList());
        }

        private static (Guid, Guid, Guid?, InventoryAlertKind) Key(InventoryAlertCondition condition) =>
            Key(condition.ItemId, condition.BranchId, condition.BatchId, condition.Kind);

        private static (Guid, Guid, Guid?, InventoryAlertKind) Key(
            Guid itemId, Guid branchId, Guid? batchId, InventoryAlertKind kind) =>
            (itemId, branchId, batchId, kind);
    }
}
