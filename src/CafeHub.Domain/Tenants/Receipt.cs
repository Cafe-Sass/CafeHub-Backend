using System.Globalization;

namespace CafeHub.Domain.Tenants
{
    public sealed class Receipt
    {
        public const int MaximumReceiptNumberLength = 32;
        public const string NeutralGuestName = "Guest";

        private readonly List<ReceiptLine> lines = [];
        private readonly List<ReceiptPayment> payments = [];

        private Receipt()
        {
        }

        public Guid Id { get; private set; }

        public Guid BillId { get; private set; }

        public Guid BranchId { get; private set; }

        public string ReceiptNumber { get; private set; } = string.Empty;

        public DateTimeOffset IssuedAt { get; private set; }

        public decimal TotalAmount { get; private set; }

        public decimal PaidAmount { get; private set; }

        public decimal RemainingAmount { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public IReadOnlyList<ReceiptLine> Lines => lines;

        public IReadOnlyList<ReceiptPayment> Payments => payments;

        public static Receipt Issue(
            Guid id,
            Guid billId,
            Guid branchId,
            string receiptNumber,
            DateTimeOffset issuedAt,
            decimal totalAmount,
            decimal paidAmount,
            decimal remainingAmount,
            IReadOnlyCollection<ReceiptLine> receiptLines,
            IReadOnlyCollection<ReceiptPayment> receiptPayments,
            Guid? actorId)
        {
            ArgumentNullException.ThrowIfNull(receiptLines);
            ArgumentNullException.ThrowIfNull(receiptPayments);
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The receipt id is required.", nameof(id));
            }

            if (billId == Guid.Empty)
            {
                throw new ArgumentException("The bill id is required.", nameof(billId));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (!IsValidReceiptNumber(receiptNumber))
            {
                throw new ArgumentException(
                    "The receipt number must look like R-<yyyyMMdd>-<number>.",
                    nameof(receiptNumber));
            }

            if (!IsValidAmount(totalAmount) || !IsValidAmount(paidAmount) || !IsValidAmount(remainingAmount))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalAmount),
                    "The receipt amounts must be non-negative, at most two decimal places, and within numeric(12,2).");
            }

            if (paidAmount <= 0m || remainingAmount != 0m || remainingAmount != totalAmount - paidAmount)
            {
                throw new ArgumentException(
                    "A receipt exists only for a fully paid bill with a positive paid amount.",
                    nameof(remainingAmount));
            }

            if (receiptLines.Count == 0 || receiptPayments.Count == 0)
            {
                throw new ArgumentException(
                    "A receipt carries at least one line and one payment snapshot.",
                    nameof(receiptLines));
            }

            var receipt = new Receipt
            {
                Id = id,
                BillId = billId,
                BranchId = branchId,
                ReceiptNumber = receiptNumber.Trim(),
                IssuedAt = issuedAt,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                RemainingAmount = remainingAmount,
                CreatedAt = issuedAt,
                CreatedBy = actorId,
            };

            foreach (var line in receiptLines)
            {
                ArgumentNullException.ThrowIfNull(line);
                line.AssignReceipt(id);
                receipt.lines.Add(line);
            }

            foreach (var payment in receiptPayments)
            {
                ArgumentNullException.ThrowIfNull(payment);
                payment.AssignReceipt(id);
                receipt.payments.Add(payment);
            }

            return receipt;
        }

        public static bool IsValidReceiptNumber(string? receiptNumber)
        {
            var trimmed = receiptNumber?.Trim();
            if (string.IsNullOrEmpty(trimmed)
                || trimmed.Length > MaximumReceiptNumberLength
                || !trimmed.StartsWith("R-", StringComparison.Ordinal))
            {
                return false;
            }

            var parts = trimmed.Split('-');
            return parts.Length == 3
                && parts[1].Length == 8
                && parts[1].All(char.IsAsciiDigit)
                && int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number >= 1;
        }

        public static string FormatReceiptNumber(DateOnly businessDate, int number) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"R-{businessDate:yyyyMMdd}-{number}");

        private static bool IsValidAmount(decimal amount) =>
            amount >= 0m
            && amount <= Payment.MaximumAmount
            && decimal.Round(amount, 2) == amount;
    }
}
