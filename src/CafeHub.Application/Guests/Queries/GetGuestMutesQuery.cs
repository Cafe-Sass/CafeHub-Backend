using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Guests.DTOs;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Guests.Queries
{
    public sealed record GetGuestMutesQuery : IQuery<OffsetPage<GuestMuteListItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public bool? Active { get; init; }

        public Guid? MutedByUserId { get; init; }
    }
}
