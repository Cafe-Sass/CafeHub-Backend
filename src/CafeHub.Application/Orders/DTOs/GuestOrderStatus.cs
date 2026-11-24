using CafeHub.Application.Orders.Contracts;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders.DTOs
{
    public sealed record GuestOrderStatus(
        int OrderNumber,
        string Status,
        string? RejectionReason,
        int? TableNumber,
        string ServiceType,
        DateTimeOffset SubmittedAt,
        DateTimeOffset ResponseDeadline,
        decimal TotalAmount,
        IReadOnlyList<GuestOrderStatusLine> Items)
    {
        public string? BillReference { get; init; }

        public static GuestOrderStatus From(GuestOrderEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);

            return new GuestOrderStatus(
                entry.OrderNumber,
                entry.Status,
                entry.Status == nameof(OrderStatus.Rejected) ? entry.RejectionReason : null,
                entry.TableNumber,
                entry.ServiceType,
                entry.SubmittedAt,
                entry.ResponseDeadline,
                entry.TotalAmount,
                entry.Items
                    .Select(item => new GuestOrderStatusLine(
                        item.ProductName,
                        item.SizeName,
                        item.Quantity,
                        item.AddonNames))
                    .ToArray())
            {
                BillReference = entry.BillReference,
            };
        }
    }
}
