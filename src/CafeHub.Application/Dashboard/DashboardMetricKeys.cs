namespace CafeHub.Application.Dashboard
{
    public static class DashboardMetricKeys
    {
        public const string Sales = "sales";
        public const string Collected = "collected";
        public const string Orders = "orders";
        public const string AverageOrderValue = "averageOrderValue";
        public const string PaymentSplit = "paymentSplit";

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
            new[] { Sales, Collected, Orders, AverageOrderValue, PaymentSplit });
    }
}
