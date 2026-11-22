using CafeHub.Application.Orders.Contracts;

namespace CafeHub.Application.Orders
{
    public static class OrderFeedStationFilter
    {
        /// <summary>
        /// Keeps the items whose station is in the visible scope or unrouted, and drops the orders that end up
        /// with no visible items. A null scope means every station is visible and the entries pass unchanged.
        /// </summary>
        public static IReadOnlyList<OrderFeedEntry> Apply(
            IReadOnlyList<OrderFeedEntry> entries,
            IReadOnlyList<Guid>? visibleStationIds)
        {
            ArgumentNullException.ThrowIfNull(entries);

            if (visibleStationIds is null)
            {
                return entries;
            }

            return [.. entries
                .Select(entry => entry with { Items = VisibleItems(entry.Items, visibleStationIds) })
                .Where(entry => entry.Items.Count > 0)];
        }

        private static IReadOnlyList<OrderFeedEntryItem> VisibleItems(
            IReadOnlyList<OrderFeedEntryItem> items,
            IReadOnlyList<Guid> visibleStationIds) => [.. items
                .Where(item => item.StationId is null || visibleStationIds.Contains(item.StationId.Value))];
    }
}
