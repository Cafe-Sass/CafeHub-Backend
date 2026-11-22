namespace CafeHub.Domain.Tenants
{
    public sealed class OrderItemAddon
    {
        public const int MaximumNameLength = 48;
        public const decimal MaximumPrice = 99_999_999.99m;

        private OrderItemAddon()
        {
        }

        public Guid Id { get; private set; }

        public Guid OrderItemId { get; private set; }

        public Guid? ProductAddonId { get; private set; }

        public string AddonName { get; private set; } = string.Empty;

        public decimal Price { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static OrderItemAddon Create(
            Guid id,
            Guid? productAddonId,
            string addonName,
            decimal price,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The add-on snapshot id is required.", nameof(id));
            }

            if (productAddonId == Guid.Empty)
            {
                throw new ArgumentException("The add-on reference cannot be empty.", nameof(productAddonId));
            }

            return new OrderItemAddon
            {
                Id = id,
                ProductAddonId = productAddonId,
                AddonName = ValidateName(addonName),
                Price = ValidatePrice(price),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public static bool IsValidName(string? name)
        {
            var trimmed = name?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumNameLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidPrice(decimal price) =>
            price >= 0m && price <= MaximumPrice && decimal.Round(price, 2) == price;

        internal void AssignOrderItem(Guid orderItemId)
        {
            if (orderItemId == Guid.Empty)
            {
                throw new ArgumentException("The order item id is required.", nameof(orderItemId));
            }

            OrderItemId = orderItemId;
        }

        private static string ValidateName(string name)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException(
                    "The add-on name must be 1 to 48 characters after trimming, with no control characters.",
                    nameof(name));
            }

            return name.Trim();
        }

        private static decimal ValidatePrice(decimal price)
        {
            if (!IsValidPrice(price))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    "The add-on price must be between 0.00 and 99999999.99 with at most two decimal places.");
            }

            return price;
        }
    }
}
