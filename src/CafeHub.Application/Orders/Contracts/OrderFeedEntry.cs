namespace CafeHub.Application.Orders.Contracts
{
    public sealed record OrderFeedEntry(
        Guid Id,
        int OrderNumber,
        int TableNumber,
        string ServiceType,
        string Status,
        string GuestDisplayName,
        DateTimeOffset SubmittedAt,
        DateTimeOffset ResponseDeadline,
        decimal TotalAmount,
        IReadOnlyList<OrderFeedEntryItem> Items);
}
