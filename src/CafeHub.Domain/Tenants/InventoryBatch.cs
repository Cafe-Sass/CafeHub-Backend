using System.Globalization;

namespace CafeHub.Domain.Tenants
{
    public sealed class InventoryBatch
    {
        public const decimal MaximumQuantity = 999_999_999.999m;
        public const int MaximumQuantityScale = InventoryItem.MaximumQuantityScale;

        private InventoryBatch()
        {
        }

        public Guid Id { get; private set; }

        public Guid ItemId { get; private set; }

        public Guid BranchId { get; private set; }

        public decimal ReceivedQuantity { get; private set; }

        public decimal RemainingQuantity { get; private set; }

        public DateOnly ReceivedDate { get; private set; }

        public DateOnly? ExpiryDate { get; private set; }

        public Guid ReceivedByUserId { get; private set; }

        public string IdempotencyKey { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static InventoryBatch Create(
            Guid id,
            Guid itemId,
            Guid branchId,
            decimal receivedQuantity,
            DateOnly receivedDate,
            DateOnly? expiryDate,
            bool itemTracksExpiry,
            Guid receivedByUserId,
            string idempotencyKey,
            DateTimeOffset now,
            Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The inventory batch id is required.", nameof(id));
            }

            if (itemId == Guid.Empty)
            {
                throw new ArgumentException("The inventory item id is required.", nameof(itemId));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (receivedByUserId == Guid.Empty)
            {
                throw new ArgumentException("The receiving user id is required.", nameof(receivedByUserId));
            }

            if (string.IsNullOrWhiteSpace(idempotencyKey))
            {
                throw new ArgumentException("The idempotency key is required.", nameof(idempotencyKey));
            }

            var validatedQuantity = ValidateQuantity(receivedQuantity);
            var validatedReceivedDate = ValidateReceivedDate(receivedDate);

            return new InventoryBatch
            {
                Id = id,
                ItemId = itemId,
                BranchId = branchId,
                ReceivedQuantity = validatedQuantity,
                RemainingQuantity = validatedQuantity,
                ReceivedDate = validatedReceivedDate,
                ExpiryDate = ValidateExpiryDate(expiryDate, validatedReceivedDate, itemTracksExpiry),
                ReceivedByUserId = receivedByUserId,
                IdempotencyKey = idempotencyKey,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public static bool IsValidQuantity(decimal quantity) =>
            quantity > 0m
            && quantity <= MaximumQuantity
            && quantity == decimal.Round(quantity, MaximumQuantityScale);

        public static bool IsValidReceivedDate(DateOnly receivedDate) => receivedDate != default;

        public static bool IsValidExpiryDate(DateOnly? expiryDate) =>
            expiryDate is not { } expiry || expiry != default;

        public static bool IsValidMovementQuantity(decimal signedQuantity) =>
            signedQuantity != 0m
            && signedQuantity <= MaximumQuantity
            && signedQuantity >= -MaximumQuantity
            && signedQuantity == decimal.Round(signedQuantity, MaximumQuantityScale);

        public bool CanApplyMovement(decimal signedQuantity)
        {
            if (!IsValidMovementQuantity(signedQuantity))
            {
                return false;
            }

            var remaining = RemainingQuantity + signedQuantity;
            return remaining >= 0m
                && remaining <= ReceivedQuantity
                && remaining == decimal.Round(remaining, MaximumQuantityScale);
        }

        public void ApplyMovement(decimal signedQuantity, DateTimeOffset now, Guid actorId)
        {
            if (!CanApplyMovement(signedQuantity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(signedQuantity),
                    "The movement must keep the remaining quantity between zero and the received quantity.");
            }

            RemainingQuantity += signedQuantity;
            UpdatedAt = now;
            UpdatedBy = actorId;
        }

        public static string FormatDate(DateOnly date) =>
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static decimal ValidateQuantity(decimal quantity)
        {
            if (!IsValidQuantity(quantity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    $"The quantity must be greater than zero and at most {MaximumQuantity} with at most {MaximumQuantityScale} decimals.");
            }

            return quantity;
        }

        private static DateOnly ValidateReceivedDate(DateOnly receivedDate)
        {
            if (!IsValidReceivedDate(receivedDate))
            {
                throw new ArgumentException("The received date is required.", nameof(receivedDate));
            }

            return receivedDate;
        }

        private static DateOnly? ValidateExpiryDate(
            DateOnly? expiryDate,
            DateOnly receivedDate,
            bool itemTracksExpiry)
        {
            if (expiryDate is not { } expiry)
            {
                if (itemTracksExpiry)
                {
                    throw new ArgumentException(
                        "The expiry date is required for an item that tracks expiry.", nameof(expiryDate));
                }

                return null;
            }

            if (!IsValidExpiryDate(expiry))
            {
                throw new ArgumentException("The expiry date is invalid.", nameof(expiryDate));
            }

            if (!itemTracksExpiry)
            {
                throw new ArgumentException(
                    "An item that does not track expiry cannot record an expiry date.", nameof(expiryDate));
            }

            if (expiry < receivedDate)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expiryDate), "The expiry date must not be before the received date.");
            }

            return expiry;
        }
    }
}
