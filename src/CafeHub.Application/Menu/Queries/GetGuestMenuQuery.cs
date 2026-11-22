using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Queries
{
    public sealed record GetGuestMenuQuery : IQuery<OffsetPage<GuestMenuItemResponse>>, ISensitiveRequest
    {
        public string? Code { get; init; }

        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public string? Sort { get; init; }

        public SortDirection SortDirection { get; init; } = SortDirection.Asc;

        public string? Search { get; init; }

        public Guid? CategoryId { get; init; }

        public ProductKind? Kind { get; init; }

        public bool? Caffeine { get; init; }

        public string? Tag { get; init; }
    }
}
