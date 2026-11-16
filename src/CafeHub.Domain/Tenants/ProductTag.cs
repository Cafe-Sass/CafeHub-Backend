namespace CafeHub.Domain.Tenants
{
    public sealed class ProductTag
    {
        public const int MaximumTagLength = 32;

        private ProductTag()
        {
        }

        public Guid ProductId { get; private set; }

        public string Tag { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        internal static ProductTag Create(Guid productId, string tag, DateTimeOffset now)
        {
            if (productId == Guid.Empty)
            {
                throw new ArgumentException("The product id is required.", nameof(productId));
            }

            return new ProductTag
            {
                ProductId = productId,
                Tag = Normalize(tag),
                CreatedAt = now,
            };
        }

        internal static string Normalize(string? tag)
        {
            var trimmed = tag?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length > MaximumTagLength)
            {
                throw new ArgumentException(
                    "The tag must be between 1 and 32 characters after trimming.",
                    nameof(tag));
            }

            if (trimmed.Any(character => !char.IsAsciiLetterLower(character)
                && !char.IsAsciiDigit(character)
                && character is not '-' and not ' '))
            {
                throw new ArgumentException(
                    "The tag may contain only lower-case letters, digits, hyphens, and spaces.",
                    nameof(tag));
            }

            return trimmed;
        }
    }
}
