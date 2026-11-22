using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables.Queries;
using MediatR;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetGuestMenuQueryHandler(
        ISender sender, IGuestMenuStore store, PaginationOptions paginationOptions)
        : IQueryHandler<GetGuestMenuQuery, OffsetPage<GuestMenuItemResponse>>
    {
        public async Task<Result<OffsetPage<GuestMenuItemResponse>>> Handle(
            GetGuestMenuQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var context = await sender.Send(new ResolveGuestQrQuery(query.Code), cancellationToken);
            if (context.IsFailure)
            {
                return Result.Failure<OffsetPage<GuestMenuItemResponse>>(context.Error!);
            }

            var pageSize = paginationOptions.ResolvePageSize(query.PageSize);
            var requestedSort = query.Sort is null
                ? null
                : new[] { new SortRequest(query.Sort, query.SortDirection) };
            var resolvedSort = ProductListSort.Whitelist.Resolve(requestedSort);
            if (resolvedSort.IsFailure)
            {
                return Result.Failure<OffsetPage<GuestMenuItemResponse>>(resolvedSort.Error!);
            }

            var page = await store.ListAsync(
                context.Value.BranchId,
                query.Search,
                query.CategoryId,
                query.Kind,
                query.Caffeine,
                query.Tag,
                query.Page,
                pageSize,
                resolvedSort.Value,
                query.IncludeTotalCount,
                cancellationToken);
            return Result.Success(page);
        }
    }
}
