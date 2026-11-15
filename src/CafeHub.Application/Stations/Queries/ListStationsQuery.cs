using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Stations.Queries
{
    public sealed record ListStationsQuery : IQuery<OffsetPage<StationResponse>>
    {
        public required Guid BranchId { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public StationStatus? Status { get; init; }
    }
}
