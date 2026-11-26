namespace CafeHub.Domain.Tenants
{
    public sealed class RefundRequestAttachment
    {
        public const int MaximumMediaKeyLength = 512;

        private RefundRequestAttachment()
        {
        }

        public Guid Id { get; private set; }

        public Guid RefundRequestId { get; private set; }

        public string MediaKey { get; private set; } = string.Empty;

        public string ContentType { get; private set; } = string.Empty;

        public long SizeBytes { get; private set; }

        public string Sha256 { get; private set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; private set; }

        public static RefundRequestAttachment Create(
            Guid id,
            Guid refundRequestId,
            string mediaKey,
            string contentType,
            long sizeBytes,
            string sha256,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty || refundRequestId == Guid.Empty)
            {
                throw new ArgumentException("Attachment identifiers are required.");
            }

            if (string.IsNullOrWhiteSpace(mediaKey) || mediaKey.Length > MaximumMediaKeyLength)
            {
                throw new ArgumentException("The media key is required and has a maximum length.", nameof(mediaKey));
            }

            if (!IsAllowedContentType(contentType))
            {
                throw new ArgumentException("The attachment content type is not allowed.", nameof(contentType));
            }

            if (sizeBytes <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sizeBytes));
            }

            if (!IsValidSha256(sha256))
            {
                throw new ArgumentException("The attachment checksum must be a lowercase SHA-256 value.", nameof(sha256));
            }

            return new RefundRequestAttachment
            {
                Id = id,
                RefundRequestId = refundRequestId,
                MediaKey = mediaKey,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                Sha256 = sha256,
                CreatedAt = createdAt,
            };
        }

        public static bool IsAllowedContentType(string? contentType) =>
            contentType is "image/jpeg" or "image/png" or "image/webp" or "application/pdf";

        public static bool IsValidSha256(string? sha256) =>
            sha256 is { Length: 64 }
            && sha256.All(character => char.IsAsciiDigit(character) || character is >= 'a' and <= 'f');
    }
}
