namespace CafeHub.Domain.Tenants
{
    public sealed class Bill
    {
        public const int PublicReferenceLength = 22;

        private Bill()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid TableId { get; private set; }

        public BillStatus Status { get; private set; }

        public string PublicReference { get; private set; } = string.Empty;

        public DateTimeOffset OpenedAt { get; private set; }

        public DateTimeOffset? ClosedAt { get; private set; }

        public Guid? ClosedByUserId { get; private set; }

        public string? CloseReason { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Bill Create(
            Guid id,
            Guid branchId,
            Guid tableId,
            string publicReference,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The bill id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (tableId == Guid.Empty)
            {
                throw new ArgumentException("The table id is required.", nameof(tableId));
            }

            return new Bill
            {
                Id = id,
                BranchId = branchId,
                TableId = tableId,
                Status = BillStatus.Open,
                PublicReference = ValidatePublicReference(publicReference),
                OpenedAt = now,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public static decimal ResolveTotalAmount(IReadOnlyCollection<Order> orders)
        {
            ArgumentNullException.ThrowIfNull(orders);
            return orders
                .Where(order => order.Status == OrderStatus.Accepted)
                .Sum(order => order.TotalAmount);
        }

        public static bool IsValidPublicReference(string? reference) =>
            reference is { Length: PublicReferenceLength }
            && reference.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

        private static string ValidatePublicReference(string reference)
        {
            if (!IsValidPublicReference(reference))
            {
                throw new ArgumentException(
                    "The bill public reference must be 22 base64url characters.", nameof(reference));
            }

            return reference;
        }
    }
}
