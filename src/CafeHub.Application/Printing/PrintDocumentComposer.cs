using System.Globalization;
using System.Text.Json;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing
{
    /// <summary>
    /// Builds the structured, profile-width document payload stored on a print job. Lines come only from
    /// the order/receipt snapshots: no voucher codes and no payment proof ever reach a printer.
    /// </summary>
    public static class PrintDocumentComposer
    {
        public const int MinimumLineWidth = 16;
        private const string TruncationMarker = "...";

        public static string ComposeOrderTicket(
            Order order,
            IReadOnlyList<OrderItem> items,
            int lineWidth)
        {
            ArgumentNullException.ThrowIfNull(order);
            ArgumentNullException.ThrowIfNull(items);
            var width = NormalizeWidth(lineWidth);
            var lines = new List<PrintPayloadLine>
            {
                Line($"ORDER TICKET #{order.OrderNumber}", width, PrintPayloadAlignments.Center, true),
                Line($"Placed {order.SubmittedAt:yyyy-MM-dd HH:mm}", width),
                Line($"Guest {order.GuestDisplayName}", width),
                Line(new string('-', width), width),
            };

            foreach (var item in items.OrderBy(item => item.LineNumber))
            {
                lines.Add(Line($"{item.Quantity}x {item.ProductName}", width, PrintPayloadAlignments.Left, true));
                if (!string.IsNullOrWhiteSpace(item.SizeName))
                {
                    lines.Add(Line($"   {item.SizeName}", width));
                }

                foreach (var addon in item.Addons)
                {
                    lines.Add(Line($"   + {addon.AddonName}", width));
                }
            }

            lines.Add(Line(new string('-', width), width));
            lines.Add(Line($"Total {FormatAmount(order.TotalAmount)}", width, PrintPayloadAlignments.Right, true));

            return Serialize(PrintDocumentType.OrderTicket, order.Id, lines);
        }

        public static string ComposeReceipt(Receipt receipt, int lineWidth)
        {
            ArgumentNullException.ThrowIfNull(receipt);
            var width = NormalizeWidth(lineWidth);
            var lines = new List<PrintPayloadLine>
            {
                Line("RECEIPT", width, PrintPayloadAlignments.Center, true),
                Line(receipt.ReceiptNumber, width, PrintPayloadAlignments.Center),
                Line($"Issued {receipt.IssuedAt:yyyy-MM-dd HH:mm}", width, PrintPayloadAlignments.Center),
                Line(new string('-', width), width),
            };

            foreach (var line in receipt.Lines)
            {
                var description = string.IsNullOrWhiteSpace(line.SizeName)
                    ? $"{line.Quantity}x {line.ProductName}"
                    : $"{line.Quantity}x {line.ProductName} ({line.SizeName})";
                lines.Add(Line(Columns(description, FormatAmount(line.LineTotal), width), width));

                if (!string.IsNullOrWhiteSpace(line.Addons))
                {
                    lines.Add(Line($"   {line.Addons}", width));
                }
            }

            lines.Add(Line(new string('-', width), width));

            foreach (var payment in receipt.Payments)
            {
                lines.Add(Line(
                    Columns(payment.Method.ToString(), FormatAmount(payment.Amount), width),
                    width));
            }

            lines.Add(Line(new string('-', width), width));
            lines.Add(Line(Columns("Total", FormatAmount(receipt.TotalAmount), width), width, PrintPayloadAlignments.Left, true));
            lines.Add(Line(Columns("Paid", FormatAmount(receipt.PaidAmount), width), width, PrintPayloadAlignments.Left, true));
            lines.Add(Line("THANK YOU", width, PrintPayloadAlignments.Center));

            return Serialize(PrintDocumentType.Receipt, receipt.BillId, lines);
        }

        private static string Serialize(PrintDocumentType documentType, Guid sourceId, IReadOnlyList<PrintPayloadLine> lines) =>
            JsonSerializer.Serialize(new PrintDocumentPayload(
                documentType.ToString(),
                sourceId.ToString(),
                lines));

        private static PrintPayloadLine Line(
            string text,
            int width,
            string alignment = PrintPayloadAlignments.Left,
            bool emphasized = false) =>
            new(Fit(text, width), alignment, emphasized);

        private static string Fit(string text, int width)
        {
            var singleLine = text.Replace('\r', ' ').Replace('\n', ' ').TrimEnd();
            if (singleLine.Length <= width)
            {
                return singleLine;
            }

            if (width <= TruncationMarker.Length)
            {
                return singleLine[..Math.Max(width, 0)];
            }

            return string.Concat(
                singleLine.AsSpan(0, width - TruncationMarker.Length),
                TruncationMarker);
        }

        private static string Columns(string left, string right, int width)
        {
            var available = width - right.Length - 1;
            var fitted = available < 1 ? string.Empty : Fit(left, available);
            return string.Concat(fitted.PadRight(Math.Max(available, 0)), " ", right);
        }

        private static string FormatAmount(decimal amount) =>
            amount.ToString("0.00", CultureInfo.InvariantCulture);

        private static int NormalizeWidth(int lineWidth) =>
            Math.Max(lineWidth, MinimumLineWidth);
    }
}
