namespace CafeHub.Application.Insights
{
    public static class BusinessInsightValidator
    {
        public const double MinimumConfidence = 0;
        public const double MaximumConfidence = 1;

        public static IReadOnlyList<BusinessInsightCard> Validate(
            BusinessInsightInput input,
            IReadOnlyList<BusinessInsightCard>? cards)
        {
            ArgumentNullException.ThrowIfNull(input);

            if (cards is null || cards.Count == 0)
            {
                return Array.Empty<BusinessInsightCard>();
            }

            var metricKeys = input.Metrics
                .Select(metric => metric.Key)
                .ToHashSet(StringComparer.Ordinal);

            return cards
                .Where(card => IsValid(card, metricKeys))
                .ToList();
        }

        private static bool IsValid(
            BusinessInsightCard card,
            HashSet<string> metricKeys)
        {
            if (string.IsNullOrWhiteSpace(card.Title)
                || string.IsNullOrWhiteSpace(card.Recommendation)
                || !double.IsFinite(card.Confidence)
                || card.Confidence < MinimumConfidence
                || card.Confidence > MaximumConfidence
                || card.Evidence is null
                || card.Evidence.Count == 0)
            {
                return false;
            }

            return card.Evidence.All(evidence =>
                !string.IsNullOrWhiteSpace(evidence.Key)
                && metricKeys.Contains(evidence.Key));
        }
    }
}
