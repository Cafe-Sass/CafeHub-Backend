namespace CafeHub.Domain.Tenants
{
    public sealed class RefundRequest
    {
        public const int PublicReferenceLength = 22;
        public const int MinimumReasonLength = 3;
        public const int MaximumReasonLength = 256;
        public const int MaximumDetailsLength = 2000;
        public const int MaximumIdempotencyKeyLength = 64;
        public const int MinimumIdempotencyKeyLength = 8;
        public const int MaximumAttachmentCount = 5;

        private readonly List<RefundRequestAttachment> attachments = [];
        private readonly List<RefundRequestEvent> events = [];

        private RefundRequest()
        {
        }

        public Guid Id { get; private set; }

        public Guid BillId { get; private set; }

        public Guid PaymentId { get; private set; }

        public Guid? OrderId { get; private set; }

        public Guid BranchId { get; private set; }

        public string PublicReference { get; private set; } = string.Empty;

        public string IdempotencyKey { get; private set; } = string.Empty;

        public RefundRequestStatus Status { get; private set; }

        public string Reason { get; private set; } = string.Empty;

        public string? Details { get; private set; }

        public decimal ClaimedAmount { get; private set; }

        public string? GuestDisplayName { get; private set; }

        public Guid? AssignedUserId { get; private set; }

        public string? ResolutionNote { get; private set; }

        public Guid? DecidedByUserId { get; private set; }

        public DateTimeOffset? DecidedAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public IReadOnlyList<RefundRequestAttachment> Attachments => attachments;

        public IReadOnlyList<RefundRequestEvent> Events => events;

        public static RefundRequest Create(
            Guid id,
            Guid submissionEventId,
            Bill bill,
            Payment payment,
            string publicReference,
            string idempotencyKey,
            string reason,
            string? details,
            decimal claimedAmount,
            decimal approvedRefundedAmount,
            string? guestDisplayName,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(bill);
            ArgumentNullException.ThrowIfNull(payment);

            if (id == Guid.Empty || submissionEventId == Guid.Empty)
            {
                throw new ArgumentException("Request identifiers are required.");
            }

            if (payment.BillId != bill.Id || payment.Status != PaymentStatus.Confirmed)
            {
                throw new ArgumentException("The payment must be confirmed and belong to the request bill.", nameof(payment));
            }

            if (!Bill.IsValidPublicReference(publicReference))
            {
                throw new ArgumentException("The public reference must be 22 base64url characters.", nameof(publicReference));
            }

            if (!IsValidIdempotencyKey(idempotencyKey))
            {
                throw new ArgumentException("The idempotency key must be 8 to 64 URL-safe characters.", nameof(idempotencyKey));
            }

            if (!IsValidReason(reason))
            {
                throw new ArgumentException("The reason must be 3 to 256 characters after trimming.", nameof(reason));
            }

            if (!IsValidDetails(details))
            {
                throw new ArgumentException("Details must not exceed 2000 characters.", nameof(details));
            }

            if (!Payment.IsValidAmount(claimedAmount)
                || !IsValidApprovedRefundedAmount(payment.Amount, approvedRefundedAmount)
                || claimedAmount > payment.Amount - approvedRefundedAmount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(claimedAmount),
                    "The claimed amount must be positive and within the payment's remaining refundable amount.");
            }

            var request = new RefundRequest
            {
                Id = id,
                BillId = bill.Id,
                PaymentId = payment.Id,
                OrderId = payment.OrderId,
                BranchId = bill.BranchId,
                PublicReference = publicReference,
                IdempotencyKey = idempotencyKey,
                Status = RefundRequestStatus.Open,
                Reason = reason.Trim(),
                Details = string.IsNullOrWhiteSpace(details) ? null : details.Trim(),
                ClaimedAmount = claimedAmount,
                GuestDisplayName = string.IsNullOrWhiteSpace(guestDisplayName) ? null : guestDisplayName.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };

            request.events.Add(RefundRequestEvent.Create(
                submissionEventId,
                id,
                RefundRequestEventKind.Submitted,
                null,
                RefundRequestStatus.Open,
                RefundRequestActorKind.Guest,
                null,
                null,
                now));
            return request;
        }

        public bool AddAttachment(RefundRequestAttachment attachment)
        {
            ArgumentNullException.ThrowIfNull(attachment);
            if (attachments.Count >= MaximumAttachmentCount
                || attachment.RefundRequestId != Id
                || attachments.Any(existing => existing.Id == attachment.Id))
            {
                return false;
            }

            attachments.Add(attachment);
            UpdatedAt = attachment.CreatedAt;
            return true;
        }

        public bool TransitionTo(
            Guid eventId,
            RefundRequestStatus target,
            Guid actorUserId,
            string? note,
            DateTimeOffset now)
        {
            if (eventId == Guid.Empty
                || actorUserId == Guid.Empty
                || !Enum.IsDefined(target)
                || !RefundRequestTransitions.IsAllowed(Status, target)
                || (RequiresResolutionNote(target) && !IsValidResolutionNote(note)))
            {
                return false;
            }

            var previousStatus = Status;
            Status = target;
            ResolutionNote = RequiresResolutionNote(target) ? note!.Trim() : null;
            DecidedByUserId = RequiresResolutionNote(target) ? actorUserId : null;
            DecidedAt = RequiresResolutionNote(target) ? now : null;
            UpdatedAt = now;
            UpdatedBy = actorUserId;
            events.Add(RefundRequestEvent.Create(
                eventId,
                Id,
                RefundRequestEventKind.StatusChanged,
                previousStatus,
                target,
                RefundRequestActorKind.Staff,
                actorUserId,
                note,
                now));
            return true;
        }

        public static bool IsValidIdempotencyKey(string? key) =>
            key is { Length: >= MinimumIdempotencyKeyLength and <= MaximumIdempotencyKeyLength }
            && key.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

        public static bool IsValidReason(string? reason)
        {
            var trimmed = reason?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length is >= MinimumReasonLength and <= MaximumReasonLength;
        }

        public static bool IsValidDetails(string? details) =>
            details is null || details.Length <= MaximumDetailsLength;

        public static bool IsValidResolutionNote(string? note) =>
            !string.IsNullOrWhiteSpace(note);

        private static bool IsValidApprovedRefundedAmount(decimal paymentAmount, decimal approvedRefundedAmount) =>
            approvedRefundedAmount >= 0m
            && approvedRefundedAmount <= paymentAmount
            && decimal.Round(approvedRefundedAmount, 2) == approvedRefundedAmount;

        private static bool RequiresResolutionNote(RefundRequestStatus status) =>
            status is RefundRequestStatus.Approved or RefundRequestStatus.Rejected;
    }
}
