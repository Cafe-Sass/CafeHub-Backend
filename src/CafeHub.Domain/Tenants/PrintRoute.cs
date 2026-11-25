namespace CafeHub.Domain.Tenants
{
    public sealed class PrintRoute
    {
        private PrintRoute()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid? StationId { get; private set; }

        public PrintDocumentType DocumentType { get; private set; }

        public Guid PrinterId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static PrintRoute Create(
            Guid id,
            Guid branchId,
            Guid? stationId,
            PrintDocumentType documentType,
            Guid printerId,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The print route id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (stationId == Guid.Empty)
            {
                throw new ArgumentException("The station id cannot be empty.", nameof(stationId));
            }

            if (!Enum.IsDefined(documentType))
            {
                throw new ArgumentOutOfRangeException(nameof(documentType), "The document type is not defined.");
            }

            if (printerId == Guid.Empty)
            {
                throw new ArgumentException("The printer id is required.", nameof(printerId));
            }

            return new PrintRoute
            {
                Id = id,
                BranchId = branchId,
                StationId = stationId,
                DocumentType = documentType,
                PrinterId = printerId,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool UpdatePrinter(Guid printerId, DateTimeOffset now, Guid? actorId)
        {
            if (printerId == Guid.Empty)
            {
                throw new ArgumentException("The printer id is required.", nameof(printerId));
            }

            if (PrinterId == printerId)
            {
                return false;
            }

            PrinterId = printerId;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }
    }
}
