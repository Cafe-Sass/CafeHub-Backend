namespace CafeHub.Domain.Tenants
{
    public sealed class ReceiptPayment
    {
        private ReceiptPayment()
        {
        }

        public Guid Id { get; private set; }

        public Guid ReceiptId { get; private set; }

        public PaymentMethod Method { get; private set; }

        public decimal Amount { get; private set; }

        public DateTimeOffset ConfirmedAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static ReceiptPayment Create(
            Guid id,
            PaymentMethod method,
            decimal amount,
            DateTimeOffset confirmedAt,
            DateTimeOffset createdAt)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The receipt payment id is required.", nameof(id));
            }

            if (!Payment.IsValidAmount(amount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    "The receipt payment amount must be greater than zero, at most two decimal places, and within numeric(12,2).");
            }

            return new ReceiptPayment
            {
                Id = id,
                Method = method,
                Amount = amount,
                ConfirmedAt = confirmedAt,
                CreatedAt = createdAt,
            };
        }

        internal void AssignReceipt(Guid receiptId)
        {
            if (receiptId == Guid.Empty)
            {
                throw new ArgumentException("The receipt id is required.", nameof(receiptId));
            }

            ReceiptId = receiptId;
        }
    }
}
