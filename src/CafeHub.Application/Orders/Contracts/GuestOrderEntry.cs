namespace CafeHub.Application.Orders.Contracts
{
    public sealed record GuestOrderEntry(
        Guid Id,
        int OrderNumber,
        int TableNumber,
        string ServiceType,
        string Status,
        string? RejectionReason,
        DateTimeOffset SubmittedAt,
        DateTimeOffset ResponseDeadline,
        decimal TotalAmount,
        IReadOnlyList<GuestOrderEntryItem> Items)
    {
        public string? BillReference { get; init; }
    }
}
