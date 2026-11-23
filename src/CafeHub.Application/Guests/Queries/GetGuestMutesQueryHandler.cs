using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Guests.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;

namespace CafeHub.Application.Guests.Queries
{
    public sealed class GetGuestMutesQueryHandler(
        IGuestMuteStore store,
        ICurrentTenantUser currentUser,
        PaginationOptions paginationOptions,
        TimeProvider timeProvider)
        : IQueryHandler<GetGuestMutesQuery, OffsetPage<GuestMuteListItem>>
    {
        public async Task<Result<OffsetPage<GuestMuteListItem>>> Handle(
            GetGuestMutesQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<OffsetPage<GuestMuteListItem>>(scope.Error!);
            }

            var branchId = scope.Value ?? query.BranchId;
            if (branchId is null)
            {
                return Result.Failure<OffsetPage<GuestMuteListItem>>(GuestMuteErrors.BranchRequired());
            }

            if (scope.Value.HasValue && query.BranchId.HasValue && query.BranchId != scope.Value)
            {
                return Result.Failure<OffsetPage<GuestMuteListItem>>(GuestMuteErrors.NotFound());
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var now = timeProvider.GetUtcNow();
            var listPage = await store.ListAsync(
                new GuestMuteListRequest(
                    query.Page,
                    pageSize,
                    query.IncludeTotalCount,
                    branchId.Value,
                    query.Active,
                    query.MutedByUserId,
                    now),
                cancellationToken);

            var page = OffsetPage<GuestMuteListItem>.Create(
                listPage.Entries.Select(entry => GuestMuteListItem.From(entry, now)).ToArray(),
                query.Page,
                pageSize,
                listPage.TotalCount);

            return Result.Success(page);
        }
    }
}
