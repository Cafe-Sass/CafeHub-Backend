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
            if (!IsValid(tag))
            {
                throw new ArgumentException(
                    "The tag must be 1 to 32 lower-case letters, digits, hyphens, or spaces after trimming.",
                    nameof(tag));
            }

            return tag!.Trim().ToLowerInvariant();
        }

        public static bool IsValid(string? tag)
        {
            var trimmed = tag?.Trim().ToLowerInvariant();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumTagLength
                && trimmed.All(character => char.IsAsciiLetterLower(character)
                    || char.IsAsciiDigit(character)
                    || character is '-' or ' ');
        }
    }
}
