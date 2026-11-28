using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Dashboard.DTOs;
using MediatR;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed class GetDashboardMetricQueryHandler(ISender sender)
        : IQueryHandler<GetDashboardMetricQuery, DashboardMetricResponse>
    {
        public async Task<Result<DashboardMetricResponse>> Handle(
            GetDashboardMetricQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var canonicalKey = DashboardMetricValues.CanonicalKey(query.Metric);
            var definition = canonicalKey is null ? null : DashboardMetricCatalog.Find(canonicalKey);
            if (definition is null)
            {
                return Result.Failure<DashboardMetricResponse>(DashboardErrors.UnknownMetric(query.Metric));
            }

            var dashboard = await sender.Send(
                new GetDashboardQuery
                {
                    From = query.From,
                    To = query.To,
                    BranchId = query.BranchId,
                },
                cancellationToken);
            if (dashboard.IsFailure)
            {
                return Result.Failure<DashboardMetricResponse>(dashboard.Error!);
            }

            return Result.Success(new DashboardMetricResponse(
                definition,
                DashboardMetricValues.Select(dashboard.Value.Metrics, definition.Key)));
        }
    }
}
