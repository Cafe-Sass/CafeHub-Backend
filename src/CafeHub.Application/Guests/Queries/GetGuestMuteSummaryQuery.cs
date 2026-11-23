using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Guests.Contracts;

namespace CafeHub.Application.Guests.Queries
{
    public sealed record GetGuestMuteSummaryQuery : IQuery<IReadOnlyList<GuestMuteSummaryEntry>>
    {
        public Guid? BranchId { get; init; }

        public DateOnly From { get; init; }

        public DateOnly To { get; init; }
    }
}
