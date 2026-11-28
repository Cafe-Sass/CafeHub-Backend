using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Dashboard.DTOs;

namespace CafeHub.Application.Dashboard.Queries
{
    public sealed record GetDashboardQuery : IQuery<DashboardResponse>
    {
        public DateOnly From { get; init; }

        public DateOnly To { get; init; }

        public Guid? BranchId { get; init; }
    }
}
