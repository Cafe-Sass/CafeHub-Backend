using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;

namespace CafeHub.Application.Dashboard
{
    public static class DashboardInsightsRules
    {
        public const int MaximumBestSellers = 20;

        public static IReadOnlyList<DashboardBestSeller> RankBestSellersByQuantity(
            IEnumerable<DashboardProductSalesRow> rows) =>
            Merge(rows)
                .OrderByDescending(seller => seller.Quantity)
                .ThenBy(seller => seller.Name, StringComparer.Ordinal)
                .ThenBy(seller => seller.ProductId)
                .Take(MaximumBestSellers)
                .ToList();

        public static IReadOnlyList<DashboardBestSeller> RankBestSellersBySales(
            IEnumerable<DashboardProductSalesRow> rows) =>
            Merge(rows)
                .OrderByDescending(seller => seller.Sales)
                .ThenBy(seller => seller.Name, StringComparer.Ordinal)
                .ThenBy(seller => seller.ProductId)
                .Take(MaximumBestSellers)
                .ToList();

        public static IReadOnlyList<DashboardPeakHour> GroupPeakHours(
            IEnumerable<DateTimeOffset> acceptedTimes,
            TimeZoneInfo timeZone)
        {
            ArgumentNullException.ThrowIfNull(acceptedTimes);
            ArgumentNullException.ThrowIfNull(timeZone);

            var counts = new int[24];
            foreach (var acceptedAt in acceptedTimes)
            {
                counts[TimeZoneInfo.ConvertTime(acceptedAt, timeZone).Hour]++;
            }

            return Enumerable.Range(0, 24)
                .Select(hour => new DashboardPeakHour(hour, counts[hour]))
                .ToList();
        }

        private static IEnumerable<DashboardBestSeller> Merge(IEnumerable<DashboardProductSalesRow> rows)
        {
            ArgumentNullException.ThrowIfNull(rows);

            return rows
                .GroupBy(row => row.ProductId)
                .Select(group => new DashboardBestSeller(
                    group.Key,
                    group.OrderByDescending(row => row.Quantity)
                        .ThenBy(row => row.ProductName, StringComparer.Ordinal)
                        .First()
                        .ProductName,
                    group.Sum(row => row.Quantity),
                    group.Sum(row => row.Sales)));
        }
    }
}
