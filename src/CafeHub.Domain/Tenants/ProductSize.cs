namespace CafeHub.Domain.Tenants
{
    public sealed class ProductSize
    {
        public const int MaximumNameLength = 32;
        public const decimal MaximumPrice = 99_999_999.99m;

        private ProductSize()
        {
        }

        public Guid Id { get; private set; }

        public Guid ProductId { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public decimal Price { get; private set; }

        public bool IsDefault { get; private set; }

        public ProductComponentStatus Status { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static ProductSize Create(
            Guid id,
            string name,
            decimal price,
            bool isDefault,
            DateTimeOffset now,
            Guid actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The size id is required.", nameof(id));
            }

            return new ProductSize
            {
                Id = id,
                Name = ValidateName(name),
                Price = ValidatePrice(price),
                IsDefault = isDefault,
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
                    "The size name must be between 1 and 32 characters after trimming.",
                    nameof(name));
            }

            if (trimmed.Any(char.IsControl))
            {
                throw new ArgumentException("The size name must not contain control characters.", nameof(name));
            }

            return trimmed;
        }

        private static decimal ValidatePrice(decimal price)
        {
            if (price < 0m || price > MaximumPrice || decimal.Round(price, 2) != price)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(price),
                    "The size price must be between 0.00 and 99999999.99 with at most two decimal places.");
            }

            return price;
        }
    }
}
