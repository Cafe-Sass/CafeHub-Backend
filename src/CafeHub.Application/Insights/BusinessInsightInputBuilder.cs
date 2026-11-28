using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.DTOs;

namespace CafeHub.Application.Insights
{
    public static class BusinessInsightInputBuilder
    {
        public static BusinessInsightInput From(
            IReadOnlyList<DashboardLeakageSignal> signals,
            IReadOnlyList<DashboardNearExpiryItem> nearExpiryItems)
        {
            ArgumentNullException.ThrowIfNull(signals);
            ArgumentNullException.ThrowIfNull(nearExpiryItems);

            var metrics = signals
                .Select(signal => new BusinessInsightMetric(signal.Key, signal.Value, signal.Count))
                .ToList();

            metrics.Add(new BusinessInsightMetric(
                DashboardMetricKeys.NearExpiryItems,
                nearExpiryItems.Count,
                nearExpiryItems.Count));

            return new BusinessInsightInput(metrics);
        }
    }
}
