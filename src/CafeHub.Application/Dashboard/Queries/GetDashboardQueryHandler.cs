using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard.DTOs;
using MediatR;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed class GetDashboardQueryHandler(ISender sender)
        : IQueryHandler<GetDashboardQuery, DashboardResponse>
    {
        public async Task<Result<DashboardResponse>> Handle(
            GetDashboardQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var revenue = await sender.Send(
                new GetDashboardRevenueQuery
                {
                    From = query.From,
                    To = query.To,
                    BranchId = query.BranchId,
                },
                cancellationToken);
            if (revenue.IsFailure)
            {
                return Result.Failure<DashboardResponse>(revenue.Error!);
            }

            var insights = await sender.Send(
                new GetDashboardSalesInsightsQuery
                {
                    From = query.From,
                    To = query.To,
                    BranchId = query.BranchId,
                },
                cancellationToken);
            if (insights.IsFailure)
            {
                return Result.Failure<DashboardResponse>(insights.Error!);
            }

            var revenueMetrics = revenue.Value.Metrics;
            var insightMetrics = insights.Value.Metrics;

            return Result.Success(new DashboardResponse(
                DashboardMetricCatalog.All,
                new DashboardOverviewMetrics(
                    revenueMetrics.Sales,
                    revenueMetrics.Collected,
                    revenueMetrics.Orders,
                    revenueMetrics.AverageOrderValue,
                    revenueMetrics.PaymentSplit,
                    insightMetrics.BestSellersByQuantity,
                    insightMetrics.BestSellersBySales,
                    insightMetrics.PeakHours,
                    insightMetrics.WifiAttachRate,
                    insightMetrics.NearExpiryItems)));
        }
    }
}
