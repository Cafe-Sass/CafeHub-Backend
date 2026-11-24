namespace CafeHub.Domain.Tenants
{
    public sealed class Payment
    {
        public const int MaximumIdempotencyKeyLength = 128;
        public const int MaximumNoteLength = 140;
        public const int MaximumMediaReferenceLength = 512;
        public const int MinimumRejectionReasonLength = 3;
        public const int MaximumRejectionReasonLength = 256;
        public const decimal MaximumAmount = 9_999_999_999.99m;

        private Payment()
        {
        }

        public Guid Id { get; private set; }

        public Guid BillId { get; private set; }

        public Guid? OrderId { get; private set; }

        public PaymentMethod Method { get; private set; }

        public PaymentStatus Status { get; private set; }

        public decimal Amount { get; private set; }

        public Guid? ConfirmedByUserId { get; private set; }

        public DateTimeOffset? ConfirmedAt { get; private set; }

        public string IdempotencyKey { get; private set; } = string.Empty;

        public string? MediaReference { get; private set; }

        public decimal? ClaimedAmount { get; private set; }

        public string? Note { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static Payment ConfirmCash(
            Guid id,
            Guid billId,
            Guid? orderId,
            decimal amount,
            Guid confirmedByUserId,
            string idempotencyKey,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The payment id is required.", nameof(id));
            }

            if (billId == Guid.Empty)
            {
                throw new ArgumentException("The bill id is required.", nameof(billId));
            }

            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("The linked order id cannot be empty.", nameof(orderId));
            }

            if (confirmedByUserId == Guid.Empty)
            {
                throw new ArgumentException("The confirming user id is required.", nameof(confirmedByUserId));
            }

            if (!IsValidAmount(amount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "The payment amount must be greater than zero, at most two decimal places, and within numeric(12,2).");
            }

            return new Payment
            {
                Id = id,
                BillId = billId,
                OrderId = orderId,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Confirmed,
                Amount = amount,
                ConfirmedByUserId = confirmedByUserId,
                ConfirmedAt = now,
                IdempotencyKey = ValidateIdempotencyKey(idempotencyKey),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public static Payment SubmitOnlineProof(
            Guid id,
            Guid billId,
            decimal claimedAmount,
            string mediaReference,
            string? note,
            string idempotencyKey,
            DateTimeOffset now)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The payment id is required.", nameof(id));
            }

            if (billId == Guid.Empty)
            {
                throw new ArgumentException("The bill id is required.", nameof(billId));
            }

            if (!IsValidAmount(claimedAmount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(claimedAmount),
                    "The claimed amount must be greater than zero, at most two decimal places, and within numeric(12,2).");
            }

            if (string.IsNullOrWhiteSpace(mediaReference)
                || mediaReference.Length > MaximumMediaReferenceLength)
            {
                throw new ArgumentException(
                    "The media reference is required and must not exceed 512 characters.",
                    nameof(mediaReference));
            }

            if (!IsValidNote(note))
            {
                throw new ArgumentException(
                    "The note must be 1 to 140 characters without control characters.",
                    nameof(note));
            }

            return new Payment
            {
                Id = id,
                BillId = billId,
                OrderId = null,
                Method = PaymentMethod.Online,
                Status = PaymentStatus.PendingReview,
                Amount = claimedAmount,
                ConfirmedByUserId = null,
                ConfirmedAt = null,
                IdempotencyKey = ValidateIdempotencyKey(idempotencyKey),
                MediaReference = mediaReference,
                ClaimedAmount = claimedAmount,
                Note = note?.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = null,
                UpdatedBy = null,
            };
        }

        public void ConfirmReview(decimal confirmedAmount, Guid actorId, DateTimeOffset now)
        {
            if (Status is not PaymentStatus.PendingReview)
            {
                throw new InvalidOperationException(
                    $"Only a payment awaiting review can be confirmed. The current status is {Status}.");
            }

            if (!IsValidAmount(confirmedAmount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(confirmedAmount),
                    "The confirmed amount must be greater than zero, at most two decimal places, and within numeric(12,2).");
            }

            if (actorId == Guid.Empty)
            {
                throw new ArgumentException("The reviewing user id is required.", nameof(actorId));
            }

            Status = PaymentStatus.Confirmed;
            Amount = confirmedAmount;
            ConfirmedByUserId = actorId;
            ConfirmedAt = now;
            UpdatedAt = now;
            UpdatedBy = actorId;
        }

        public void RejectReview(Guid actorId, DateTimeOffset now)
        {
            if (Status is not PaymentStatus.PendingReview)
            {
                throw new InvalidOperationException(
                    $"Only a payment awaiting review can be rejected. The current status is {Status}.");
            }

            if (actorId == Guid.Empty)
            {
                throw new ArgumentException("The reviewing user id is required.", nameof(actorId));
            }

            Status = PaymentStatus.Rejected;
            UpdatedAt = now;
            UpdatedBy = actorId;
        }

        public static bool IsValidRejectionReason(string? reason)
        {
            var trimmed = reason?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length >= MinimumRejectionReasonLength
                && trimmed.Length <= MaximumRejectionReasonLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidAmount(decimal amount) =>
            amount > 0 && amount <= MaximumAmount && decimal.Round(amount, 2) == amount;

        public static bool IsValidIdempotencyKey(string? key)
        {
            var trimmed = key?.Trim();
            return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaximumIdempotencyKeyLength;
        }

        public static bool IsValidNote(string? note)
        {
            var trimmed = note?.Trim();
            return trimmed is null
                || (trimmed.Length >= 1
                    && trimmed.Length <= MaximumNoteLength
                    && !trimmed.Any(char.IsControl));
        }

        public static decimal ResolvePaidAmount(IReadOnlyCollection<Payment> payments)
        {
            ArgumentNullException.ThrowIfNull(payments);
            return payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed)
                .Sum(payment => payment.Amount);
        }

        public static decimal ResolveBillRemainingAmount(decimal billTotalAmount, IReadOnlyCollection<Payment> payments) =>
            billTotalAmount - ResolvePaidAmount(payments);

        public static decimal ResolveOrderRemainingAmount(Order order, IReadOnlyCollection<Payment> payments)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(payments);
            return order.TotalAmount - payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed && payment.OrderId == order.Id)
                .Sum(payment => payment.Amount);
        }

        public static PaymentSettlement ResolveSettlementType(IReadOnlyCollection<Payment> payments)
        {
            ArgumentNullException.ThrowIfNull(payments);
            var methods = payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed)
                .Select(payment => payment.Method)
                .Distinct()
                .ToList();
            return methods.Count switch
            {
                1 when methods[0] == PaymentMethod.Online => PaymentSettlement.Online,
                2 => PaymentSettlement.Hybrid,
                _ => PaymentSettlement.Cash,
            };
        }

        private static string ValidateIdempotencyKey(string key)
        {
            if (!IsValidIdempotencyKey(key))
            {
                throw new ArgumentException(
                    "The payment idempotency key must be 1 to 128 characters.", nameof(key));
            }

            return key.Trim();
        }
    }
}
