using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Application.Orders.DTOs
{
    public sealed record OrderFeedItem(
        Guid Id,
        int OrderNumber,
        int TableNumber,
        string ServiceType,
        string Status,
        string GuestDisplayName,
        DateTimeOffset SubmittedAt,
        DateTimeOffset ResponseDeadline,
        decimal TotalAmount,
        IReadOnlyList<OrderFeedItemLine> Items)
    {
        public static OrderFeedItem From(OrderFeedEntry entry) => new(
            entry.Id,
            entry.OrderNumber,
            entry.TableNumber,
            entry.ServiceType,
            entry.Status,
            entry.GuestDisplayName,
            entry.SubmittedAt,
            entry.ResponseDeadline,
            entry.TotalAmount,
            entry.Items
                .Select(item => new OrderFeedItemLine(
                    item.ProductName,
                    item.SizeName,
                    item.Quantity,
                    item.StationName,
                    item.AddonNames))
                .ToArray());
    }
}
