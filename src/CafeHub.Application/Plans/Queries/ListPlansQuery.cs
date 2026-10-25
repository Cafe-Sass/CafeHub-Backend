using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Plans.DTOs;

namespace CafeHub.Application.Plans.Queries
{
    public sealed record ListPlansQuery : IQuery<OffsetPage<PlanResponse>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public string? Sort { get; init; }

        public SortDirection SortDirection { get; init; } = SortDirection.Asc;
    }
}
