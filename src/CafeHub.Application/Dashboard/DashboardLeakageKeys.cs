namespace CafeHub.Application.Dashboard
{
    public static class DashboardLeakageKeys
    {
        public const string RejectedAndExpiredOrders = "rejectedAndExpiredOrders";
        public const string BillsClosedWithAmountOwed = "billsClosedWithAmountOwed";
        public const string RejectedPaymentProofs = "rejectedPaymentProofs";
        public const string SalesCollectedGap = "salesCollectedGap";

        public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(
            new[]
            {
                RejectedAndExpiredOrders,
                BillsClosedWithAmountOwed,
                RejectedPaymentProofs,
                SalesCollectedGap,
            });
    }
}
