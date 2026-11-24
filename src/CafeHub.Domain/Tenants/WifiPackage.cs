namespace CafeHub.Domain.Tenants
{
    public sealed class WifiPackage
    {
        public const int MaximumNameLength = 48;
        public const int MaximumDurationMinutes = 43_200;
        public const decimal MaximumSpeedMbps = 999_999.99m;
        public const int MaximumQuotaMb = 1_000_000;
        public const decimal MaximumPrice = 9_999_999_999.99m;

        private WifiPackage()
        {
        }

        public Guid Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public int DurationMinutes { get; private set; }

        public decimal? SpeedMbps { get; private set; }

        public WifiDataMode DataMode { get; private set; }

        public int? QuotaMb { get; private set; }

        public decimal Price { get; private set; }

        public int SortOrder { get; private set; }

        public WifiPackageStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static WifiPackage Create(
            Guid id,
            string name,
            int durationMinutes,
            decimal? speedMbps,
            WifiDataMode dataMode,
            int? quotaMb,
            decimal price,
            int sortOrder,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The WiFi package id is required.", nameof(id));
            }

            var package = new WifiPackage
            {
                Id = id,
                Status = WifiPackageStatus.Active,
                CreatedAt = now,
                CreatedBy = actorId,
            };
            package.Apply(name, durationMinutes, speedMbps, dataMode, quotaMb, price, sortOrder, now, actorId);
            return package;
        }

        public bool Update(
            string name,
            int durationMinutes,
            decimal? speedMbps,
            WifiDataMode dataMode,
            int? quotaMb,
            decimal price,
            int sortOrder,
            DateTimeOffset now,
            Guid? actorId)
        {
            var trimmedName = name?.Trim() ?? string.Empty;
            if (Name == trimmedName
                && DurationMinutes == durationMinutes
                && SpeedMbps == speedMbps
                && DataMode == dataMode
                && QuotaMb == quotaMb
                && Price == price
                && SortOrder == sortOrder)
            {
                return false;
            }

            Apply(name, durationMinutes, speedMbps, dataMode, quotaMb, price, sortOrder, now, actorId);
            return true;
        }

        public bool Deactivate(DateTimeOffset now, Guid? actorId) =>
            ChangeStatus(WifiPackageStatus.Inactive, now, actorId);

        public bool Reactivate(DateTimeOffset now, Guid? actorId) =>
            ChangeStatus(WifiPackageStatus.Active, now, actorId);

        public static bool IsValidName(string? name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumNameLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidDuration(int durationMinutes) =>
            durationMinutes is >= 1 and <= MaximumDurationMinutes;

        public static bool IsValidSpeed(decimal? speedMbps) =>
            speedMbps is null
            || (speedMbps > 0m
                && speedMbps <= MaximumSpeedMbps
                && decimal.Round(speedMbps.Value, 2) == speedMbps.Value);

        public static bool IsValidQuota(int? quotaMb) =>
            quotaMb is null || quotaMb is >= 1 and <= MaximumQuotaMb;

        public static bool IsValidPrice(decimal price) =>
            price >= 0m
            && price <= MaximumPrice
            && decimal.Round(price, 2) == price;

        public static bool IsValidSortOrder(int sortOrder) => sortOrder >= 0;

        private bool ChangeStatus(WifiPackageStatus status, DateTimeOffset now, Guid? actorId)
        {
            if (!Enum.IsDefined(status))
            {
                throw new ArgumentOutOfRangeException(nameof(status), "The WiFi package status is not defined.");
            }

            if (Status == status)
            {
                return false;
            }

            Status = status;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        private void Apply(
            string name,
            int durationMinutes,
            decimal? speedMbps,
            WifiDataMode dataMode,
            int? quotaMb,
            decimal price,
            int sortOrder,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    $"The WiFi package name must be 1 to {MaximumNameLength} characters without control characters.",
                    nameof(name));
            }

            if (!IsValidDuration(durationMinutes))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationMinutes),
                    $"The duration must be between 1 and {MaximumDurationMinutes} minutes.");
            }

            if (!IsValidSpeed(speedMbps))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedMbps),
                    $"The speed limit must be null or greater than zero, at most two decimal places, and at most {MaximumSpeedMbps} Mbps.");
            }

            if (!Enum.IsDefined(dataMode))
            {
                throw new ArgumentOutOfRangeException(nameof(dataMode), "The WiFi data mode is not defined.");
            }

            if (!IsValidQuota(quotaMb))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quotaMb),
                    $"The quota must be null or between 1 and {MaximumQuotaMb} MB.");
            }

            if (dataMode == WifiDataMode.Limited && quotaMb is null)
            {
                throw new ArgumentException("A limited data mode requires a quota in MB.", nameof(quotaMb));
            }

            if (dataMode == WifiDataMode.Unlimited && quotaMb is not null)
            {
                throw new ArgumentException("An unlimited data mode cannot carry a quota.", nameof(quotaMb));
            }

            if (!IsValidPrice(price))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    $"The price must be non-negative, at most two decimal places, and within numeric(12,2).");
            }

            if (!IsValidSortOrder(sortOrder))
            {
                throw new ArgumentOutOfRangeException(nameof(sortOrder), "The sort order cannot be negative.");
            }

            Name = name.Trim();
            DurationMinutes = durationMinutes;
            SpeedMbps = speedMbps;
            DataMode = dataMode;
            QuotaMb = quotaMb;
            Price = price;
            SortOrder = sortOrder;
            UpdatedAt = now;
            UpdatedBy = actorId;
        }
    }
}
