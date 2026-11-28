using CafeHub.Application.Dashboard;

namespace CafeHub.Application.Insights
{
    public static class BusinessInsightRules
    {
        public const int MaximumCards = 10;

        public const double SignalConfidence = 0.7;

        public const double GapConfidence = 0.6;

        public const string RejectedAndExpiredOrdersTitle = "Rejected and expired orders need a review";

        public const string BillsClosedWithAmountOwedTitle = "Bills were closed with money still owed";

        public const string RejectedPaymentProofsTitle = "Payment proofs were rejected";

        public const string SalesCollectedGapTitle = "Sales and collected amounts do not match";

        public const string NearExpiryItemsTitle = "Stock is close to its expiry date";

        public static IReadOnlyList<BusinessInsightCard> Generate(BusinessInsightInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var metrics = input.Metrics
                .GroupBy(metric => metric.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            var cards = new List<BusinessInsightCard>();
            Add(cards, metrics, DashboardLeakageKeys.RejectedAndExpiredOrders, RejectedAndExpiredOrdersTitle,
                "Check the rejected and expired orders and confirm whether stock or response time caused them.",
                SignalConfidence);
            Add(cards, metrics, DashboardLeakageKeys.BillsClosedWithAmountOwed, BillsClosedWithAmountOwedTitle,
                "Review the closed bills with a remaining amount and confirm the payment with the guest.",
                SignalConfidence);
            Add(cards, metrics, DashboardLeakageKeys.RejectedPaymentProofs, RejectedPaymentProofsTitle,
                "Review the rejected payment proofs and confirm the payment with the guest.",
                SignalConfidence);
            Add(cards, metrics, DashboardLeakageKeys.SalesCollectedGap, SalesCollectedGapTitle,
                "Compare the bills closed in the range with the confirmed payments and record the difference.",
                GapConfidence);
            Add(cards, metrics, DashboardMetricKeys.NearExpiryItems, NearExpiryItemsTitle,
                "Plan a promotion or internal use for the near-expiry batches before they expire.",
                SignalConfidence);

            return cards.Take(MaximumCards).ToList();
        }

        private static void Add(
            List<BusinessInsightCard> cards,
            IReadOnlyDictionary<string, BusinessInsightMetric> metrics,
            string key,
            string title,
            string recommendation,
            double confidence)
        {
            if (!metrics.TryGetValue(key, out var metric))
            {
                return;
            }

            var isPresent = key == DashboardLeakageKeys.SalesCollectedGap
                ? metric.Value > 0
                : metric.Count > 0;

            if (!isPresent)
            {
                return;
            }

            cards.Add(new BusinessInsightCard(
                title,
                new[] { new BusinessInsightEvidence(metric.Key, metric.Value) },
                recommendation,
                confidence));
        }
    }
}
