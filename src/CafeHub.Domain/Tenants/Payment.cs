namespace CafeHub.Domain.Tenants
{
    public sealed class Payment
    {
        public const int MaximumIdempotencyKeyLength = 128;
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

        public static bool IsValidAmount(decimal amount) =>
            amount > 0 && amount <= MaximumAmount && decimal.Round(amount, 2) == amount;

        public static bool IsValidIdempotencyKey(string? key)
        {
            var trimmed = key?.Trim();
            return !string.IsNullOrEmpty(trimmed) && trimmed.Length <= MaximumIdempotencyKeyLength;
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
