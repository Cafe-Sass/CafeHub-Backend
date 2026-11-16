namespace CafeHub.Domain.Tenants
{
    public sealed class ProductAddon
    {
        public const int MaximumNameLength = 48;
        public const decimal MaximumPrice = 99_999_999.99m;

        private ProductAddon()
        {
        }

        public Guid Id { get; private set; }

        public Guid ProductId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public decimal Price { get; private set; }

        public ProductComponentStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static ProductAddon Create(
            Guid id,
            string name,
            decimal price,
            DateTimeOffset now,
            Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The add-on id is required.", nameof(id));
            }

            return new ProductAddon
            {
                Id = id,
                Name = ValidateName(name),
                Price = ValidatePrice(price),
                Status = ProductComponentStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        internal void AssignProduct(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                throw new ArgumentException("The product id is required.", nameof(productId));
            }

            ProductId = productId;
        }

        internal void Deactivate(DateTimeOffset now, Guid actorId)
        {
            Status = ProductComponentStatus.Inactive;
            UpdatedAt = now;
            UpdatedBy = actorId;
        }

        private static string ValidateName(string name)
        {
            var trimmed = name?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumNameLength)
            {
                throw new ArgumentException(
                    "The add-on name must be between 1 and 48 characters after trimming.",
                    nameof(name));
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException("The add-on name must not contain control characters.", nameof(name));
            }

            return trimmed;
        }

        private static decimal ValidatePrice(decimal price)
        {
            if (price < 0m || price > MaximumPrice || decimal.Round(price, 2) != price)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    "The add-on price must be between 0.00 and 99999999.99 with at most two decimal places.");
            }

            return price;
        }
    }
}
