namespace CafeHub.Domain.Tenants
{
    public sealed class BranchProductAvailability
    {
        private BranchProductAvailability()
        {
        }

        public Guid BranchId { get; private set; }

        public Guid ProductId { get; private set; }

        public bool IsAvailable { get; private set; }

        public string ReasonCode { get; private set; } = BranchProductAvailabilityReasons.Manual;

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static BranchProductAvailability Create(
            Guid branchId,
            Guid productId,
            bool isAvailable,
            string reasonCode,
            DateTimeOffset now,
            Guid actorId)
        {
            ValidateId(branchId, nameof(branchId));
            ValidateId(productId, nameof(productId));

            return new BranchProductAvailability
            {
                BranchId = branchId,
                ProductId = productId,
                IsAvailable = isAvailable,
                ReasonCode = ValidateReasonCode(reasonCode),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool Set(bool isAvailable, string reasonCode, DateTimeOffset now, Guid actorId)
        {
            var validatedReason = ValidateReasonCode(reasonCode);
            if (isAvailable == IsAvailable && validatedReason == ReasonCode)
            {
                return false;
            }

            IsAvailable = isAvailable;
            ReasonCode = validatedReason;
            UpdatedAt = now;
            UpdatedBy = actorId;
            return true;
        }

        private static string ValidateReasonCode(string reasonCode)
        {
            if (!BranchProductAvailabilityReasons.IsValid(reasonCode))
            {
                throw new ArgumentException(
                    "The availability reason must be manual, out_of_stock, or expired.", nameof(reasonCode));
            }

            return reasonCode;
        }

        private static void ValidateId(Guid id, string parameterName)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The identifier is required.", parameterName);
            }
        }
    }
}
