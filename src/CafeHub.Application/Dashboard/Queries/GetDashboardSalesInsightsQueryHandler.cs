using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed class GetDashboardSalesInsightsQueryHandler(
        IDashboardInsightsStore store,
        IWifiSettingsStore wifiSettingsStore,
        ICafeSettingsReader settingsReader,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetDashboardSalesInsightsQuery, DashboardSalesInsightsResponse>
    {
        public async Task<Result<DashboardSalesInsightsResponse>> Handle(
            GetDashboardSalesInsightsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<DashboardSalesInsightsResponse>(scope.Error!);
            }

            var settings = await settingsReader.GetAsync(cancellationToken);
            if (settings is null || string.IsNullOrWhiteSpace(settings.Timezone))
            {
                return Result.Failure<DashboardSalesInsightsResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return Result.Failure<DashboardSalesInsightsResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            var range = DashboardDateRange.Resolve(query.From, query.To, timeZone);
            if (range.IsFailure)
            {
                return Result.Failure<DashboardSalesInsightsResponse>(range.Error!);
            }

            var wifiSettings = await wifiSettingsStore.GetAsync(null, cancellationToken);
            var snapshot = await store.GetAsync(
                new DashboardInsightsRequest(scope.Value ?? query.BranchId, range.Value.From, range.Value.To),
                cancellationToken);

            return Result.Success(new DashboardSalesInsightsResponse(
                DashboardMetricCatalog.All,
                new DashboardSalesInsightsMetrics(
                    DashboardInsightsRules.RankBestSellersByQuantity(snapshot.ProductSales),
                    DashboardInsightsRules.RankBestSellersBySales(snapshot.ProductSales),
                    DashboardInsightsRules.GroupPeakHours(snapshot.AcceptedOrderTimes, timeZone),
                    DashboardInsightsRules.ResolveWifiAttachRate(
                        wifiSettings.Policy,
                        snapshot.AcceptedDineInOrders,
                        snapshot.AcceptedDineInOrdersWithWifiPackage),
                    OrderNearExpiry(snapshot.NearExpiryItems))));
        }

        private static IReadOnlyList<DashboardNearExpiryItem> OrderNearExpiry(
            IReadOnlyList<DashboardNearExpiryRow> items) =>
            items
                .OrderBy(item => item.ExpiryDate)
                .ThenBy(item => item.ItemName, StringComparer.Ordinal)
                .ThenBy(item => item.BatchId)
                .Select(item => new DashboardNearExpiryItem(
                    item.ItemId,
                    item.ItemName,
                    item.BranchId,
                    item.BatchId,
                    item.ExpiryDate))
                .ToList();
    }
}
