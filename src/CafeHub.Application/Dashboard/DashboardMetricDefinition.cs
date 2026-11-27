namespace CafeHub.Application.Dashboard
{
    public sealed record DashboardMetricDefinition(
        string Key,
        string Label,
        string Formula,
        IReadOnlyList<string> Includes,
        IReadOnlyList<string> Excludes);
}
