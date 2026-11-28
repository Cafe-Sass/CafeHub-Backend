namespace CafeHub.Application.Insights.DTOs
{
    public sealed record InsightsEvidence(
        string Key,
        decimal Value);

    public sealed record InsightsCard(
        string Title,
        IReadOnlyList<InsightsEvidence> Evidence,
        string Recommendation,
        double Confidence);

    public sealed record InsightsResponse(
        string Source,
        DateTimeOffset GeneratedAt,
        IReadOnlyList<InsightsCard> Cards,
        string? Reason);
}
