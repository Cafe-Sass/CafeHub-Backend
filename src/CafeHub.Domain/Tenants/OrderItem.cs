namespace CafeHub.Domain.Tenants
{
    public sealed class OrderItem
    {
        public const int MaximumNameLength = 96;
        public const int MaximumSizeNameLength = 32;
        public const int MaximumQuantity = 50;
        public const int MaximumAddonCount = 10;
        public const decimal MaximumPrice = 99_999_999.99m;

        private readonly List<OrderItemAddon> addons = [];

        private OrderItem()
        {
        }

        public Guid Id { get; private set; }

        public Guid OrderId { get; private set; }

        public int LineNumber { get; private set; }

        public Guid? ProductId { get; private set; }

        public Guid? ProductSizeId { get; private set; }

        public Guid? StationId { get; private set; }

        public string ProductName { get; private set; } = string.Empty;

        public string SizeName { get; private set; } = string.Empty;

        public decimal UnitPrice { get; private set; }

        public int Quantity { get; private set; }

        public decimal LineTotal { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public IReadOnlyList<OrderItemAddon> Addons => addons;

        public static OrderItem Create(
            Guid id,
            int lineNumber,
            Guid? productId,
            Guid? productSizeId,
            Guid? stationId,
            string productName,
            string sizeName,
            decimal unitPrice,
            int quantity,
            IReadOnlyCollection<OrderItemAddon> itemAddons,
            DateTimeOffset now,
            Guid? actorId)
        {
            ArgumentNullException.ThrowIfNull(itemAddons);
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The order item id is required.", nameof(id));
            }

            if (lineNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lineNumber), "The line number must start at 1.");
            }

            ValidateReference(productId, nameof(productId));
            ValidateReference(productSizeId, nameof(productSizeId));
            ValidateReference(stationId, nameof(stationId));
            var validatedUnitPrice = ValidatePrice(unitPrice);
            var validatedQuantity = ValidateQuantity(quantity);

            if (itemAddons.Count > MaximumAddonCount)
            {
                throw new ArgumentException($"An order line accepts at most {MaximumAddonCount} add-ons.", nameof(itemAddons));
            }

            var seenAddonIds = new HashSet<Guid>();
            foreach (var addon in itemAddons)
            {
                ArgumentNullException.ThrowIfNull(addon);
                if (!seenAddonIds.Add(addon.Id))
                {
                    throw new ArgumentException("Add-on snapshots must be unique within an order line.", nameof(itemAddons));
                }
            }

            var item = new OrderItem
            {
                Id = id,
                LineNumber = lineNumber,
                ProductId = productId,
                ProductSizeId = productSizeId,
                StationId = stationId,
                ProductName = ValidateProductName(productName),
                SizeName = ValidateSizeName(sizeName),
                UnitPrice = validatedUnitPrice,
                Quantity = validatedQuantity,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };

            foreach (var addon in itemAddons)
            {
                addon.AssignOrderItem(id);
                item.addons.Add(addon);
            }

            item.LineTotal = item.ComputeLineTotal();
            return item;
        }

        public static bool IsValidProductName(string? name) =>
            IsValidText(name, MaximumNameLength);

        public static bool IsValidSizeName(string? name) =>
            IsValidText(name, MaximumSizeNameLength);

        public static bool IsValidUnitPrice(decimal price) =>
            price >= 0m && price <= MaximumPrice && decimal.Round(price, 2) == price;

        public static bool IsValidQuantity(int quantity) => quantity is >= 1 and <= MaximumQuantity;

        internal void AssignOrder(Guid orderId)
        {
            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("The order id is required.", nameof(orderId));
            }

            OrderId = orderId;
        }

        private decimal ComputeLineTotal() =>
            UnitPrice * Quantity + addons.Sum(addon => addon.Price);

        private static void ValidateReference(Guid? reference, string parameterName)
        {
            if (reference == Guid.Empty)
            {
                throw new ArgumentException("The snapshot reference cannot be empty.", parameterName);
            }
        }

        private static decimal ValidatePrice(decimal price)
        {
            if (!IsValidUnitPrice(price))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    "The unit price must be between 0.00 and 99999999.99 with at most two decimal places.");
            }

            return price;
        }

        private static int ValidateQuantity(int quantity)
        {
            if (!IsValidQuantity(quantity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    $"The quantity must be between 1 and {MaximumQuantity}.");
            }

            return quantity;
        }

        private static string ValidateProductName(string name)
        {
            if (!IsValidProductName(name))
            {
                throw new ArgumentException(
                    "The product name must be 1 to 96 characters after trimming, with no control characters.",
                    nameof(name));
            }

            return name.Trim();
        }

        private static string ValidateSizeName(string name)
        {
            if (!IsValidSizeName(name))
            {
                throw new ArgumentException(
                    "The size name must be 1 to 32 characters after trimming, with no control characters.",
                    nameof(name));
            }

            return name.Trim();
        }

        private static bool IsValidText(string? value, int maximumLength)
        {
            var trimmed = value?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= maximumLength
                && !trimmed.Any(char.IsControl);
        }
    }
}
