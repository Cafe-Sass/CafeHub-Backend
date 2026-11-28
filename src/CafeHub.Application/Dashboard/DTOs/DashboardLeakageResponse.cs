namespace CafeHub.Application.Dashboard.DTOs
{
    public sealed record DashboardLeakageContributor(
        Guid Id,
        decimal Total,
        DateTimeOffset OccurredAt);

    public sealed record DashboardLeakageSignal(
        string Key,
        int Count,
        decimal Value,
        int ContributorPage,
        bool HasMoreContributors,
        IReadOnlyList<DashboardLeakageContributor> Contributors);

    public sealed record DashboardLeakageResponse(
        string Notice,
        IReadOnlyList<DashboardMetricDefinition> Definitions,
        IReadOnlyList<DashboardLeakageSignal> Signals);
}
