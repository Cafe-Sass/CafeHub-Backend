namespace CafeHub.Domain.Subscriptions
{
    public sealed class SubscriptionRenewalRequest
    {
        public const int MaximumDecisionReferenceLength = 256;

        private SubscriptionRenewalRequest()
        {
        }

        public Guid Id { get; private set; }

        public Guid SubscriptionId { get; private set; }

        public Guid PlanVersionId { get; private set; }

        public decimal ProposedPrice { get; private set; }

        public string ProposedCurrencyCode { get; private set; } = string.Empty;

        public RenewalRequestStatus Status { get; private set; }

        public DateTimeOffset RequestedAt { get; private set; }

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset? DecidedAt { get; private set; }

        public string? DecidedBy { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static SubscriptionRenewalRequest Create(
            Guid id,
            Guid subscriptionId,
            Guid planVersionId,
            decimal proposedPrice,
            string proposedCurrencyCode,
            DateTimeOffset requestedAt,
            DateTimeOffset expiresAt,
            Guid? createdBy = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The renewal request id is required.", nameof(id));
            }

            if (subscriptionId == Guid.Empty)
            {
                throw new ArgumentException("The subscription id is required.", nameof(subscriptionId));
            }

            ValidateTerms(planVersionId, proposedPrice, proposedCurrencyCode);

            if (expiresAt <= requestedAt)
            {
                throw new ArgumentException(
                    "The renewal request expiry must be after its request time.", nameof(expiresAt));
            }

            return new SubscriptionRenewalRequest
            {
                Id = id,
                SubscriptionId = subscriptionId,
                PlanVersionId = planVersionId,
                ProposedPrice = proposedPrice,
                ProposedCurrencyCode = proposedCurrencyCode,
                Status = RenewalRequestStatus.Pending,
                RequestedAt = requestedAt,
                ExpiresAt = expiresAt,
                CreatedAt = requestedAt,
                UpdatedAt = requestedAt,
                CreatedBy = createdBy,
                UpdatedBy = createdBy,
            };
        }

        public bool IsExpired(DateTimeOffset now) =>
            Status == RenewalRequestStatus.Pending && now >= ExpiresAt;

        public bool Approve(DateTimeOffset now, string? decidedBy)
        {
            if (Status != RenewalRequestStatus.Pending || now >= ExpiresAt)
            {
                return false;
            }

            Status = RenewalRequestStatus.Approved;
            DecidedAt = now;
            DecidedBy = NormalizeDecisionReference(decidedBy);
            UpdatedAt = now;
            return true;
        }

        public bool Decline(DateTimeOffset now, string? decidedBy)
        {
            if (Status != RenewalRequestStatus.Pending || now >= ExpiresAt)
            {
                return false;
            }

            Status = RenewalRequestStatus.Declined;
            DecidedAt = now;
            DecidedBy = NormalizeDecisionReference(decidedBy);
            UpdatedAt = now;
            return true;
        }

        public bool Expire(DateTimeOffset now)
        {
            if (Status != RenewalRequestStatus.Pending || now < ExpiresAt)
            {
                return false;
            }

            Status = RenewalRequestStatus.Expired;
            DecidedAt = now;
            DecidedBy = null;
            UpdatedAt = now;
            return true;
        }

        public bool Complete(DateTimeOffset now)
        {
            if (Status != RenewalRequestStatus.Approved)
            {
                return false;
            }

            Status = RenewalRequestStatus.Completed;
            UpdatedAt = now;
            return true;
        }

        public bool ChangePlan(
            Guid planVersionId,
            decimal proposedPrice,
            string proposedCurrencyCode,
            DateTimeOffset now)
        {
            if (Status != RenewalRequestStatus.Pending)
            {
                return false;
            }

            ValidateTerms(planVersionId, proposedPrice, proposedCurrencyCode);
            PlanVersionId = planVersionId;
            ProposedPrice = proposedPrice;
            ProposedCurrencyCode = proposedCurrencyCode;
            UpdatedAt = now;
            return true;
        }

        private static void ValidateTerms(
            Guid planVersionId,
            decimal proposedPrice,
            string proposedCurrencyCode)
        {
            if (planVersionId == Guid.Empty)
            {
                throw new ArgumentException("The plan version id is required.", nameof(planVersionId));
            }

            if (proposedPrice < 0m)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(proposedPrice), "The proposed price must be zero or greater.");
            }

            if (!IsWellFormedCurrencyCode(proposedCurrencyCode))
            {
                throw new ArgumentException(
                    "The proposed currency code must be three lowercase letters.",
                    nameof(proposedCurrencyCode));
            }
        }

        private static bool IsWellFormedCurrencyCode(string? currencyCode) =>
            currencyCode is { Length: 3 }
            && currencyCode.All(character => char.IsBetween(character, 'a', 'z'));

        private static string? NormalizeDecisionReference(string? decidedBy)
        {
            if (decidedBy is null)
            {
                return null;
            }

            var trimmed = decidedBy.Trim();
            if (trimmed.Length == 0
                || trimmed.Length > MaximumDecisionReferenceLength
                || trimmed.Any(char.IsControl))
            {
                throw new ArgumentException(
                    $"The decision reference must be 1 to {MaximumDecisionReferenceLength} characters without control characters.",
                    nameof(decidedBy));
            }

            return trimmed;
        }
    }
}
