using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Insights.DTOs;

namespace CafeHub.Application.Insights.Queries
{
    public sealed record GetInsightsQuery : IQuery<InsightsResponse>
    {
        public DateOnly From { get; init; }

        public DateOnly To { get; init; }

        public Guid? BranchId { get; init; }
    }
}
