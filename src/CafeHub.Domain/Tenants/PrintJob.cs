namespace CafeHub.Domain.Tenants
{
    public sealed class PrintJob
    {
        public const int MaximumSourceTypeLength = 32;
        public const int MaximumDedupeKeyLength = 160;
        public const int MaximumPayloadLength = 16_384;
        public const int DefaultMaxAttempts = 3;

        private PrintJob()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid PrinterId { get; private set; }

        public PrintDocumentType DocumentType { get; private set; }

        public string SourceType { get; private set; } = string.Empty;

        public Guid SourceId { get; private set; }

        public string DedupeKey { get; private set; } = string.Empty;

        public PrintJobStatus Status { get; private set; }

        public int Attempts { get; private set; }

        public int MaxAttempts { get; private set; }

        public string? LastErrorCode { get; private set; }

        public string DocumentPayload { get; private set; } = string.Empty;

        public DateTimeOffset? LeaseExpiresAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public static PrintJob Create(
            Guid id,
            Guid branchId,
            Guid printerId,
            PrintDocumentType documentType,
            string sourceType,
            Guid sourceId,
            string documentPayload,
            DateTimeOffset now)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The print job id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (printerId == Guid.Empty)
            {
                throw new ArgumentException("The printer id is required.", nameof(printerId));
            }

            if (!Enum.IsDefined(documentType))
            {
                throw new ArgumentOutOfRangeException(nameof(documentType), "The document type is not defined.");
            }

            if (!IsValidSourceType(sourceType))
            {
                throw new ArgumentException(
                    $"The source type must be 1 to {MaximumSourceTypeLength} characters without control characters.",
                    nameof(sourceType));
            }

            if (sourceId == Guid.Empty)
            {
                throw new ArgumentException("The source id is required.", nameof(sourceId));
            }

            if (!IsValidPayload(documentPayload))
            {
                throw new ArgumentException(
                    $"The document payload must be 1 to {MaximumPayloadLength} characters.",
                    nameof(documentPayload));
            }

            return new PrintJob
            {
                Id = id,
                BranchId = branchId,
                PrinterId = printerId,
                DocumentType = documentType,
                SourceType = sourceType.Trim(),
                SourceId = sourceId,
                DedupeKey = BuildDedupeKey(documentType, sourceId, printerId),
                Status = PrintJobStatus.Queued,
                Attempts = 0,
                MaxAttempts = DefaultMaxAttempts,
                DocumentPayload = documentPayload,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        public static string BuildDedupeKey(
            PrintDocumentType documentType,
            Guid sourceId,
            Guid printerId) =>
            $"{documentType}:{sourceId}:{printerId}";

        public static bool IsValidSourceType(string? sourceType)
        {
            var trimmed = sourceType?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumSourceTypeLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidPayload(string? documentPayload) =>
            !string.IsNullOrWhiteSpace(documentPayload)
            && documentPayload.Length <= MaximumPayloadLength;
    }
}
