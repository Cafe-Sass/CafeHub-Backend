using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Orders.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Orders.Queries
{
    public sealed record GetOrderFeedQuery : IQuery<OffsetPage<OrderFeedItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public string? Status { get; init; }

        public DateTimeOffset? UpdatedSince { get; init; }
    }
}
