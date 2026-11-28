namespace CafeHub.Application.Insights
{
    public sealed record BusinessInsightEvidence(
        string Key,
        decimal Value);

    public sealed record BusinessInsightCard(
        string Title,
        IReadOnlyList<BusinessInsightEvidence> Evidence,
        string Recommendation,
        double Confidence);
}
