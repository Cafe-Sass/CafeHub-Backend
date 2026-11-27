using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Dashboard
{
    public static class DashboardInsightsRules
    {
        public const int MaximumBestSellers = 20;

        public const string WifiPolicyFreeReason =
            "The WiFi attach rate is available only while the cafe WiFi policy is Paid.";

        public const string NoDineInOrdersReason =
            "There were no Accepted dine-in orders in the range.";

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

        public static DashboardWifiAttachRate ResolveWifiAttachRate(
            WifiPolicy policy,
            int dineInOrders,
            int dineInOrdersWithWifiPackage)
        {
            if (policy != WifiPolicy.Paid)
            {
                return new DashboardWifiAttachRate(null, WifiPolicyFreeReason);
            }

            if (dineInOrders == 0)
            {
                return new DashboardWifiAttachRate(null, NoDineInOrdersReason);
            }

            return new DashboardWifiAttachRate(
                decimal.Round((decimal)dineInOrdersWithWifiPackage / dineInOrders, 4),
                null);
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
