using System.Globalization;

namespace CafeHub.Domain.Tenants
{
    public sealed class StockMovement
    {
        public const decimal MaximumQuantity = InventoryBatch.MaximumQuantity;
        public const int MaximumQuantityScale = InventoryBatch.MaximumQuantityScale;
        public const int MaximumReasonLength = 500;
        public const int MinimumIdempotencyKeyLength = 8;
        public const int MaximumIdempotencyKeyLength = 64;

        private StockMovement()
        {
        }

        public Guid Id { get; private set; }

        public Guid ItemId { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid BatchId { get; private set; }

        public StockMovementType Type { get; private set; }

        public decimal Quantity { get; private set; }

        public string? Reason { get; private set; }

        public string IdempotencyKey { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public static StockMovement Create(
            Guid id,
            Guid itemId,
            Guid branchId,
            Guid batchId,
            StockMovementType type,
            decimal quantity,
            string? reason,
            string idempotencyKey,
            DateTimeOffset now,
            Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The stock movement id is required.", nameof(id));
            }

            if (itemId == Guid.Empty)
            {
                throw new ArgumentException("The inventory item id is required.", nameof(itemId));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (batchId == Guid.Empty)
            {
                throw new ArgumentException("The inventory batch id is required.", nameof(batchId));
            }

            if (actorId == Guid.Empty)
            {
                throw new ArgumentException("The actor user id is required.", nameof(actorId));
            }

            if (idempotencyKey.Length < MinimumIdempotencyKeyLength
                || idempotencyKey.Length > MaximumIdempotencyKeyLength)
            {
                throw new ArgumentException(
                    $"The idempotency key must be {MinimumIdempotencyKeyLength} to {MaximumIdempotencyKeyLength} characters.",
                    nameof(idempotencyKey));
            }

            if (!IsValidQuantity(type, quantity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "The movement quantity must respect its type, stay within the fixed precision, and never be zero.");
            }

            return new StockMovement
            {
                Id = id,
                ItemId = itemId,
                BranchId = branchId,
                BatchId = batchId,
                Type = type,
                Quantity = quantity,
                Reason = ValidateReason(type, reason),
                IdempotencyKey = idempotencyKey,
                CreatedAt = now,
                CreatedBy = actorId,
            };
        }

        public static bool IsManualType(StockMovementType type) =>
            type is StockMovementType.Consume or StockMovementType.Waste or StockMovementType.Adjust;

        public static bool RequiresReason(StockMovementType type) =>
            Enum.IsDefined(type) && type != StockMovementType.Receive;

        public static bool IsValidQuantity(StockMovementType type, decimal quantity)
        {
            if (!Enum.IsDefined(type)
                || quantity == 0m
                || quantity > MaximumQuantity
                || quantity < -MaximumQuantity
                || quantity != decimal.Round(quantity, MaximumQuantityScale))
            {
                return false;
            }

            return type switch
            {
                StockMovementType.Receive => quantity > 0m,
                StockMovementType.Consume or StockMovementType.Waste => quantity < 0m,
                StockMovementType.Adjust => true,
                _ => false,
            };
        }

        public static bool IsValidReason(string? reason) =>
            reason is null
            || (reason.Trim().Length is >= 1 and <= MaximumReasonLength && !reason.Any(char.IsControl));

        public static string FormatQuantity(decimal quantity) =>
            quantity.ToString("0.###", CultureInfo.InvariantCulture);

        private static string? ValidateReason(StockMovementType type, string? reason)
        {
            if (!RequiresReason(type))
            {
                if (reason is not null)
                {
                    throw new ArgumentException(
                        "A generated receive movement cannot carry a reason.", nameof(reason));
                }

                return null;
            }

            if (!IsValidReason(reason) || reason is null)
            {
                throw new ArgumentException(
                    $"The movement reason must be 1 to {MaximumReasonLength} characters without control characters.",
                    nameof(reason));
            }

            return reason.Trim();
        }
    }
}
