using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches.Queries
{
    public sealed record ListBranchesQuery : IQuery<OffsetPage<BranchResponse>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public string? Sort { get; init; }

        public SortDirection SortDirection { get; init; } = SortDirection.Asc;

        public BranchLifecycleStatus? Status { get; init; }
    }
}
