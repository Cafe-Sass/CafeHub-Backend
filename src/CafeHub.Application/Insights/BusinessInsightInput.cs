namespace CafeHub.Application.Insights
{
    public sealed record BusinessInsightMetric(
        string Key,
        decimal Value,
        int Count);

    public sealed record BusinessInsightInput(
        IReadOnlyList<BusinessInsightMetric> Metrics)
    {
        public static BusinessInsightInput Empty { get; } = new(Array.Empty<BusinessInsightMetric>());
    }
}
