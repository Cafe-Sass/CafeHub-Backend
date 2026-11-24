namespace CafeHub.Domain.Tenants
{
    public sealed class ReceiptLine
    {
        public const int MaximumAddonsTextLength = 512;

        private ReceiptLine()
        {
        }

        public Guid Id { get; private set; }

        public Guid ReceiptId { get; private set; }

        public int OrderNumber { get; private set; }

        public string GuestDisplayName { get; private set; } = string.Empty;

        public string ProductName { get; private set; } = string.Empty;

        public string SizeName { get; private set; } = string.Empty;

        public string? Addons { get; private set; }

        public int Quantity { get; private set; }

        public decimal UnitPrice { get; private set; }

        public decimal LineTotal { get; private set; }

        public string? WifiCode { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static ReceiptLine Create(
            Guid id,
            int orderNumber,
            string? guestDisplayName,
            string productName,
            string sizeName,
            string? addons,
            int quantity,
            decimal unitPrice,
            decimal lineTotal,
            DateTimeOffset createdAt,
            string? wifiCode = null)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The receipt line id is required.", nameof(id));
            }

            if (orderNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(orderNumber), "The order number must start at 1.");
            }

            if (!OrderItem.IsValidProductName(productName))
            {
                throw new ArgumentException(
                    "The receipt line product name must be 1 to 96 characters after trimming.",
                    nameof(productName));
            }

            if (!OrderItem.IsValidSizeName(sizeName))
            {
                throw new ArgumentException(
                    "The receipt line size name must be 1 to 32 characters after trimming.",
                    nameof(sizeName));
            }

            if (!IsValidAddons(addons))
            {
                throw new ArgumentException(
                    "The receipt line add-ons must be 1 to 512 characters without control characters.",
                    nameof(addons));
            }

            if (!OrderItem.IsValidQuantity(quantity))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    $"The quantity must be between 1 and {OrderItem.MaximumQuantity}.");
            }

            if (!OrderItem.IsValidUnitPrice(unitPrice) || !IsValidLineTotal(lineTotal))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unitPrice),
                    "The receipt line prices must be between 0.00 and 99999999.99 with at most two decimal places.");
            }

            if (wifiCode is not null && !WifiVoucher.IsValidCode(wifiCode))
            {
                throw new ArgumentException(
                    $"The receipt WiFi code must be {WifiVoucher.MinimumCodeLength} to {WifiVoucher.MaximumCodeLength} letters, digits, or hyphens.",
                    nameof(wifiCode));
            }

            return new ReceiptLine
            {
                Id = id,
                OrderNumber = orderNumber,
                GuestDisplayName = NormalizeGuestName(guestDisplayName),
                ProductName = productName.Trim(),
                SizeName = sizeName.Trim(),
                Addons = addons?.Trim(),
                Quantity = quantity,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                WifiCode = wifiCode?.Trim(),
                CreatedAt = createdAt,
            };
        }

        public static bool IsValidAddons(string? addons)
        {
            var trimmed = addons?.Trim();
            return trimmed is null
                || (trimmed.Length >= 1
                    && trimmed.Length <= MaximumAddonsTextLength
                    && !trimmed.Any(char.IsControl));
        }

        public static string NormalizeGuestName(string? guestDisplayName)
        {
            var trimmed = guestDisplayName?.Trim();
            return string.IsNullOrEmpty(trimmed) ? Receipt.NeutralGuestName : trimmed;
        }

        internal void AssignReceipt(Guid receiptId)
        {
            if (receiptId == Guid.Empty)
            {
                throw new ArgumentException("The receipt id is required.", nameof(receiptId));
            }

            ReceiptId = receiptId;
        }

        private static bool IsValidLineTotal(decimal lineTotal) =>
            lineTotal >= 0m
            && lineTotal <= OrderItem.MaximumPrice
            && decimal.Round(lineTotal, 2) == lineTotal;
    }
}
