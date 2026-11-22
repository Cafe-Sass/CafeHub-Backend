using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Menu.Queries
{
    public sealed record ListBranchProductAvailabilityQuery : IQuery<OffsetPage<BranchProductAvailabilityItem>>
    {
        public Guid BranchId { get; init; }

        public bool? Available { get; init; }

        public Guid? CategoryId { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }
    }
}
