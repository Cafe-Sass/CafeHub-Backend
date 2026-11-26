namespace CafeHub.Domain.Tenants
{
    public sealed class InventoryAlert
    {
        private InventoryAlert()
        {
        }

        public Guid Id { get; private set; }

        public Guid ItemId { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid? BatchId { get; private set; }

        public InventoryAlertKind Kind { get; private set; }

        public InventoryAlertState State { get; private set; }

        public DateTimeOffset RaisedAt { get; private set; }

        public DateTimeOffset? ResolvedAt { get; private set; }

        public DateTimeOffset? NotifiedAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static InventoryAlert Raise(
            Guid id,
            Guid itemId,
            Guid branchId,
            Guid? batchId,
            InventoryAlertKind kind,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The inventory alert id is required.", nameof(id));
            }

            if (itemId == Guid.Empty)
            {
                throw new ArgumentException("The inventory item id is required.", nameof(itemId));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (!Enum.IsDefined(kind))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(kind), "The inventory alert kind is not defined.");
            }

            if (RequiresBatch(kind))
            {
                if (batchId is not { } selectedBatchId || selectedBatchId == Guid.Empty)
                {
                    throw new ArgumentException(
                        "A near-expiry or expired alert requires the inventory batch.", nameof(batchId));
                }
            }
            else if (batchId is not null)
            {
                throw new ArgumentException(
                    "A low-stock alert is raised for the whole item and carries no batch.", nameof(batchId));
            }

            return new InventoryAlert
            {
                Id = id,
                ItemId = itemId,
                BranchId = branchId,
                BatchId = batchId,
                Kind = kind,
                State = InventoryAlertState.Open,
                RaisedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public static bool RequiresBatch(InventoryAlertKind kind) =>
            kind is InventoryAlertKind.NearExpiry or InventoryAlertKind.Expired;

        public bool Resolve(DateTimeOffset now, Guid? actorId)
        {
            if (State == InventoryAlertState.Resolved)
            {
                return false;
            }

            State = InventoryAlertState.Resolved;
            ResolvedAt = now;
            Touch(now, actorId);
            return true;
        }

        public bool MarkNotified(DateTimeOffset now, Guid? actorId)
        {
            if (NotifiedAt is not null)
            {
                return false;
            }

            NotifiedAt = now;
            Touch(now, actorId);
            return true;
        }

        private void Touch(DateTimeOffset now, Guid? actorId)
        {
            UpdatedAt = now;
            UpdatedBy = actorId;
        }
    }
}
