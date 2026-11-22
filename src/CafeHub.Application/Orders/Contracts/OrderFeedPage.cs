namespace CafeHub.Application.Orders.Contracts
{
    public sealed record OrderFeedPage(IReadOnlyList<OrderFeedEntry> FetchedItems, int? TotalCount);
}
