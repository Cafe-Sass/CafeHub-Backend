using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu.Queries
{
    public sealed class GetGuestMenuQueryHandlerTests
    {
        private const string Code = "Abcdefghijklmnopqrstuv";

        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly RecordingSender sender = new();
        private readonly FakeGuestMenuStore store = new();

        [Fact]
        public async Task InvalidQrCodeReturnsTheResolveNotFoundWithoutReadingTheMenu()
        {
            sender.Response = Result.Failure<GuestQrContext>(GuestQrErrors.NotFound());

            var result = await CreateHandler().Handle(new GetGuestMenuQuery { Code = Code }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code);
            Assert.Empty(store.Calls);
            var resolved = Assert.IsType<ResolveGuestQrQuery>(sender.LastRequest);
            Assert.Equal(Code, resolved.Code);
        }

        [Fact]
        public async Task TheQrBranchScopesTheMenu()
        {
            sender.Response = Result.Success(Context());

            await CreateHandler().Handle(new GetGuestMenuQuery { Code = Code }, default);

            Assert.Equal(BranchId, Assert.Single(store.Calls).BranchId);
        }

        [Fact]
        public async Task DefaultSortResolvesToSortOrderThenId()
        {
            sender.Response = Result.Success(Context());

            await CreateHandler().Handle(new GetGuestMenuQuery { Code = Code, Page = 2 }, default);

            var call = Assert.Single(store.Calls);
            Assert.Equal(2, call.Page);
            Assert.Equal(20, call.PageSize);
            Assert.Equal(2, call.Sort.Count);
            Assert.Equal("sortOrder", call.Sort[0].Field);
            Assert.Equal(SortDirection.Asc, call.Sort[0].Direction);
            Assert.Equal("id", call.Sort[1].Field);
            Assert.Equal(SortDirection.Asc, call.Sort[1].Direction);
        }

        [Fact]
        public async Task ExplicitSortResolvesWithIdTiebreaker()
        {
            sender.Response = Result.Success(Context());

            await CreateHandler().Handle(
                new GetGuestMenuQuery { Code = Code, Sort = "name", SortDirection = SortDirection.Desc }, default);

            var call = Assert.Single(store.Calls);
            Assert.Equal("name", call.Sort[0].Field);
            Assert.Equal(SortDirection.Desc, call.Sort[0].Direction);
            Assert.Equal("id", call.Sort[1].Field);
            Assert.Equal(SortDirection.Desc, call.Sort[1].Direction);
        }

        [Fact]
        public async Task UnknownSortValueIsRejectedBeforeTheMenuIsRead()
        {
            sender.Response = Result.Success(Context());

            var result = await CreateHandler().Handle(
                new GetGuestMenuQuery { Code = Code, Sort = "bestselling" }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal("Pagination.SortFieldNotAllowed", result.Error!.Code);
            Assert.Empty(store.Calls);
        }

        [Fact]
        public async Task FiltersAndPagingArePassedThroughToTheStore()
        {
            sender.Response = Result.Success(Context());
            var categoryId = Guid.NewGuid();
            var query = new GetGuestMenuQuery
            {
                Code = Code,
                Search = "latte",
                CategoryId = categoryId,
                Kind = ProductKind.Drink,
                Caffeine = true,
                Tag = "hot",
                Page = 3,
                PageSize = 5,
                IncludeTotalCount = true,
            };

            await CreateHandler().Handle(query, default);

            var call = Assert.Single(store.Calls);
            Assert.Equal("latte", call.Search);
            Assert.Equal(categoryId, call.CategoryId);
            Assert.Equal(ProductKind.Drink, call.Kind);
            Assert.True(call.Caffeine);
            Assert.Equal("hot", call.Tag);
            Assert.Equal(3, call.Page);
            Assert.Equal(5, call.PageSize);
            Assert.True(call.IncludeTotalCount);
        }

        private GetGuestMenuQueryHandler CreateHandler() =>
            new(sender, store, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        private static GuestQrContext Context() =>
            new("Cafe", BranchId, "Downtown", Guid.NewGuid(), 5);

        private sealed class FakeGuestMenuStore : IGuestMenuStore
        {
            public List<Call> Calls { get; } = [];

            public Task<OffsetPage<GuestMenuItemResponse>> ListAsync(
                Guid branchId,
                string? search,
                Guid? categoryId,
                ProductKind? kind,
                bool? caffeine,
                string? tag,
                int page,
                int pageSize,
                IReadOnlyList<SortTerm> sort,
                bool includeTotalCount,
                CancellationToken cancellationToken)
            {
                Calls.Add(new Call(branchId, search, categoryId, kind, caffeine, tag, page, pageSize, sort,
                    includeTotalCount));
                return Task.FromResult(OffsetPage<GuestMenuItemResponse>.Create([], page, pageSize, null));
            }
        }

        private sealed record Call(
            Guid BranchId,
            string? Search,
            Guid? CategoryId,
            ProductKind? Kind,
            bool? Caffeine,
            string? Tag,
            int Page,
            int PageSize,
            IReadOnlyList<SortTerm> Sort,
            bool IncludeTotalCount);
    }
}
