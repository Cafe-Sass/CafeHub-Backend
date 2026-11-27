namespace CafeHub.Application.Dashboard
{
    public static class DashboardMetricKeys
    {
        public const string Sales = "sales";
        public const string Collected = "collected";
        public const string Orders = "orders";
        public const string AverageOrderValue = "averageOrderValue";
        public const string PaymentSplit = "paymentSplit";
        public const string BestSellers = "bestSellers";
        public const string PeakHours = "peakHours";
        public const string WifiAttachRate = "wifiAttachRate";
        public const string NearExpiryItems = "nearExpiryItems";

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
            new[]
            {
                Sales,
                Collected,
                Orders,
                AverageOrderValue,
                PaymentSplit,
                BestSellers,
                PeakHours,
                WifiAttachRate,
                NearExpiryItems,
            });
    }
}
