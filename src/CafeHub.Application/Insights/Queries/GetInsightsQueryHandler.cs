using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.Dashboard;
using CafeHub.Application.Dashboard.Contracts;
using CafeHub.Application.Dashboard.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Insights.DTOs;

namespace CafeHub.Application.Insights.Queries
{
    public sealed class GetInsightsQueryHandler(
        IDashboardLeakageStore leakageStore,
        IDashboardInsightsStore insightsStore,
        BusinessInsightGenerator generator,
        ICafeSettingsReader settingsReader,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : IQueryHandler<GetInsightsQuery, InsightsResponse>
    {
        private const int FirstContributorPage = 1;

        public async Task<Result<InsightsResponse>> Handle(
            GetInsightsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<InsightsResponse>(scope.Error!);
            }

            var settings = await settingsReader.GetAsync(cancellationToken);
            if (settings is null || string.IsNullOrWhiteSpace(settings.Timezone))
            {
                return Result.Failure<InsightsResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            TimeZoneInfo timeZone;
            try
            {
                timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.Timezone);
            }
            catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return Result.Failure<InsightsResponse>(DashboardErrors.TimeZoneUnavailable());
            }

            var range = DashboardDateRange.Resolve(query.From, query.To, timeZone);
            if (range.IsFailure)
            {
                return Result.Failure<InsightsResponse>(range.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            var leakage = await leakageStore.GetAsync(
                new DashboardLeakageRequest(branchId, range.Value.From, range.Value.To),
                cancellationToken);
            var insights = await insightsStore.GetAsync(
                new DashboardInsightsRequest(branchId, range.Value.From, range.Value.To),
                cancellationToken);

            var nearExpiry = insights.NearExpiryItems
                .Select(row => new DashboardNearExpiryItem(
                    row.ItemId,
                    row.ItemName,
                    row.BranchId,
                    row.BatchId,
                    row.ExpiryDate))
                .ToList();

            var generated = await generator.GenerateAsync(
                BusinessInsightInputBuilder.From(Signals(leakage), nearExpiry),
                cancellationToken);

            var cards = generated.Cards
                .Take(BusinessInsightRules.MaximumCards)
                .Select(card => new InsightsCard(
                    card.Title,
                    card.Evidence
                        .Select(evidence => new InsightsEvidence(evidence.Key, evidence.Value))
                        .ToList(),
                    card.Recommendation,
                    card.Confidence))
                .ToList();

            return Result.Success(new InsightsResponse(
                generated.Source,
                timeProvider.GetUtcNow(),
                cards,
                cards.Count == 0 ? InsightsReasons.NoSignals : null));
        }

        private static IReadOnlyList<DashboardLeakageSignal> Signals(
            DashboardLeakageSnapshot leakage) =>
            new[]
            {
                DashboardLeakageRules.RejectedAndExpiredOrders(
                    leakage.RejectedAndExpiredOrders,
                    FirstContributorPage),
                DashboardLeakageRules.BillsClosedWithAmountOwed(
                    leakage.ClosedBills,
                    FirstContributorPage),
                DashboardLeakageRules.RejectedPaymentProofs(
                    leakage.RejectedPaymentProofs,
                    FirstContributorPage),
                DashboardLeakageRules.SalesCollectedGap(
                    leakage.ClosedBills,
                    FirstContributorPage),
            };
    }
}
