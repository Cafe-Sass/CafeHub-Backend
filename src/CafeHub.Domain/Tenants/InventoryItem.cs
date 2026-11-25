using System.Globalization;

namespace CafeHub.Domain.Tenants
{
    public sealed class InventoryItem
    {
        public const int MaximumNameLength = 96;
        public const int DefaultNearExpiryDays = 7;
        public const int MaximumQuantityScale = 3;
        public const decimal MaximumLowStockThreshold = 999_999_999.999m;

        private InventoryItem()
        {
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public InventoryUnit Unit { get; private set; }

        public decimal LowStockThreshold { get; private set; }

        public bool TracksExpiry { get; private set; }

        public int NearExpiryDays { get; private set; }

        public InventoryItemStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static InventoryItem Create(
            Guid id,
            string name,
            InventoryUnit unit,
            decimal lowStockThreshold,
            bool tracksExpiry,
            int nearExpiryDays,
            DateTimeOffset now,
            Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The inventory item id is required.", nameof(id));
            }

            return new InventoryItem
            {
                Id = id,
                Name = ValidateName(name),
                Unit = ValidateUnit(unit),
                LowStockThreshold = ValidateLowStockThreshold(lowStockThreshold),
                TracksExpiry = tracksExpiry,
                NearExpiryDays = ValidateNearExpiryDays(nearExpiryDays),
                Status = InventoryItemStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool Update(
            string name,
            InventoryUnit unit,
            decimal lowStockThreshold,
            bool tracksExpiry,
            int nearExpiryDays,
            DateTimeOffset now,
            Guid actorId)
        {
            var validatedName = ValidateName(name);
            var validatedUnit = ValidateUnit(unit);
            var validatedThreshold = ValidateLowStockThreshold(lowStockThreshold);
            var validatedNearExpiryDays = ValidateNearExpiryDays(nearExpiryDays);

            if (Name == validatedName
                && Unit == validatedUnit
                && LowStockThreshold == validatedThreshold
                && TracksExpiry == tracksExpiry
                && NearExpiryDays == validatedNearExpiryDays)
            {
                return false;
            }

            Name = validatedName;
            Unit = validatedUnit;
            LowStockThreshold = validatedThreshold;
            TracksExpiry = tracksExpiry;
            NearExpiryDays = validatedNearExpiryDays;
            Touch(now, actorId);
            return true;
        }

        public bool Deactivate(DateTimeOffset now, Guid actorId) =>
            ChangeStatus(InventoryItemStatus.Inactive, now, actorId);

        public bool Reactivate(DateTimeOffset now, Guid actorId) =>
            ChangeStatus(InventoryItemStatus.Active, now, actorId);

        public static bool IsValidName(string? name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumNameLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidUnit(InventoryUnit unit) => Enum.IsDefined(unit);

        public static bool IsValidLowStockThreshold(decimal lowStockThreshold) =>
            lowStockThreshold >= 0m
            && lowStockThreshold <= MaximumLowStockThreshold
            && lowStockThreshold == decimal.Round(lowStockThreshold, MaximumQuantityScale);

        public static bool IsValidNearExpiryDays(int? nearExpiryDays) => nearExpiryDays is null or >= 0;

        public static string FormatQuantity(decimal quantity) =>
            quantity.ToString("0.###", CultureInfo.InvariantCulture);

        private bool ChangeStatus(InventoryItemStatus status, DateTimeOffset now, Guid actorId)
        {
            if (!Enum.IsDefined(status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), "The inventory item status is not defined.");
            }

            if (Status == status)
            {
                return false;
            }

            Status = status;
            Touch(now, actorId);
            return true;
        }

        private static string ValidateName(string name)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    $"The inventory item name must be 1 to {MaximumNameLength} characters without control characters.",
                    nameof(name));
            }

            return name.Trim();
        }

        private static InventoryUnit ValidateUnit(InventoryUnit unit)
        {
            if (!IsValidUnit(unit))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unit),
                    "The unit is not in the supported unit set.");
            }

            return unit;
        }

        private static decimal ValidateLowStockThreshold(decimal lowStockThreshold)
        {
            if (!IsValidLowStockThreshold(lowStockThreshold))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lowStockThreshold),
                    $"The low-stock threshold must be between 0 and {MaximumLowStockThreshold} with at most {MaximumQuantityScale} decimals.");
            }

            return lowStockThreshold;
        }

        private static int ValidateNearExpiryDays(int nearExpiryDays)
        {
            if (!IsValidNearExpiryDays(nearExpiryDays))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nearExpiryDays),
                    "The near-expiry days must not be negative.");
            }

            return nearExpiryDays;
        }

        private void Touch(DateTimeOffset now, Guid actorId)
        {
            UpdatedAt = now;
            UpdatedBy = actorId;
        }
    }
}
