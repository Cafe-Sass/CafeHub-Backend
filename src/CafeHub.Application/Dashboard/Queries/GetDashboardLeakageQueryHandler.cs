using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed class GetDashboardLeakageQueryHandler(
        IDashboardLeakageStore store,
        ICafeSettingsReader settingsReader,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetDashboardLeakageQuery, DashboardLeakageResponse>
    {
        public async Task<Result<DashboardLeakageResponse>> Handle(
            GetDashboardLeakageQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<DashboardLeakageResponse>(scope.Error!);
            }

            var settings = await settingsReader.GetAsync(cancellationToken);
            if (settings is null || string.IsNullOrWhiteSpace(settings.Timezone))
            {
                return Result.Failure<DashboardLeakageResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return Result.Failure<DashboardLeakageResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            var range = DashboardDateRange.Resolve(query.From, query.To, timeZone);
            if (range.IsFailure)
            {
                return Result.Failure<DashboardLeakageResponse>(range.Error!);
            }

            var snapshot = await store.GetAsync(
                new DashboardLeakageRequest(scope.Value ?? query.BranchId, range.Value.From, range.Value.To),
                cancellationToken);

            return Result.Success(new DashboardLeakageResponse(
                DashboardLeakageRules.SignalNotice,
                DashboardLeakageCatalog.All,
                new[]
                {
                    DashboardLeakageRules.RejectedAndExpiredOrders(
                        snapshot.RejectedAndExpiredOrders,
                        query.ContributorPage),
                    DashboardLeakageRules.BillsClosedWithAmountOwed(
                        snapshot.ClosedBills,
                        query.ContributorPage),
                    DashboardLeakageRules.RejectedPaymentProofs(
                        snapshot.RejectedPaymentProofs,
                        query.ContributorPage),
                    DashboardLeakageRules.SalesCollectedGap(
                        snapshot.ClosedBills,
                        query.ContributorPage),
                }));
        }
    }
}
