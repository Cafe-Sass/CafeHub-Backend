namespace CafeHub.Application.Insights
{
    public sealed record BusinessInsightResult(
        string Source,
        IReadOnlyList<BusinessInsightCard> Cards);
}
