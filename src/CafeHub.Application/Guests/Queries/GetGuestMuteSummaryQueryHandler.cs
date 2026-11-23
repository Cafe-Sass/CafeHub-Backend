using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Guests.Queries
{
    public sealed class GetGuestMuteSummaryQueryHandler(
        IGuestMuteStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetGuestMuteSummaryQuery, IReadOnlyList<GuestMuteSummaryEntry>>
    {
        public async Task<Result<IReadOnlyList<GuestMuteSummaryEntry>>> Handle(
            GetGuestMuteSummaryQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<IReadOnlyList<GuestMuteSummaryEntry>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<IReadOnlyList<GuestMuteSummaryEntry>>(GuestMuteErrors.BranchRequired());
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<IReadOnlyList<GuestMuteSummaryEntry>>(GuestMuteErrors.NotFound());
            }

            var from = new DateTimeOffset(query.From.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var toExclusive = new DateTimeOffset(query.To.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            var entries = await store.ListSummaryAsync(
                new GuestMuteSummaryRequest(branchId.Value, from, toExclusive),
                cancellationToken);

            return Result.Success(entries);
        }
    }
}
