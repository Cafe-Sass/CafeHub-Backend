using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed class GetDashboardRevenueQueryHandler(
        IDashboardRevenueStore store,
        ICafeSettingsReader settingsReader,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetDashboardRevenueQuery, DashboardRevenueResponse>
    {
        public async Task<Result<DashboardRevenueResponse>> Handle(
            GetDashboardRevenueQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<DashboardRevenueResponse>(scope.Error!);
            }

            var settings = await settingsReader.GetAsync(cancellationToken);
            if (settings is null || string.IsNullOrWhiteSpace(settings.Timezone))
            {
                return Result.Failure<DashboardRevenueResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return Result.Failure<DashboardRevenueResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            var range = DashboardDateRange.Resolve(query.From, query.To, timeZone);
            if (range.IsFailure)
            {
                return Result.Failure<DashboardRevenueResponse>(range.Error!);
            }

            var snapshot = await store.GetAsync(
                new DashboardRevenueRequest(scope.Value ?? query.BranchId, range.Value.From, range.Value.To),
                cancellationToken);

            return Result.Success(new DashboardRevenueResponse(
                DashboardMetricCatalog.All,
                new DashboardRevenueMetrics(
                    snapshot.Sales,
                    snapshot.Collected,
                    snapshot.Orders,
                    ResolveAverageOrderValue(snapshot),
                    new DashboardPaymentSplit(snapshot.CashCollected, snapshot.OnlineCollected))));
        }

        private static decimal? ResolveAverageOrderValue(DashboardRevenueSnapshot snapshot) =>
            snapshot.NonWifiOrders == 0
                ? null
                : decimal.Round(snapshot.Sales / snapshot.NonWifiOrders, 2);
    }
}
