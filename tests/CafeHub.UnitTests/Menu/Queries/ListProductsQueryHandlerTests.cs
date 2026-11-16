using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Branches;
using CafeHub.UnitTests.Menu.Commands;

namespace CafeHub.UnitTests.Menu.Queries
{
    public sealed class ListProductsQueryHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        private readonly FakeProductStore store = new();
        private readonly FakeCurrentTenantUser currentUser = new() { TenantId = TenantId, UserId = ActorId };
        private readonly StubTenantContextAccessor tenantContext = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));

        [Fact]
        public async Task DefaultSortResolvesToSortOrderThenId()
        {
            var handler = new ListProductsQueryHandler(store, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

            var result = await handler.Handle(new ListProductsQuery { Page = 2 }, default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Listed);
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
            var handler = new ListProductsQueryHandler(store, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

            var result = await handler.Handle(
                new ListProductsQuery { Sort = "name", SortDirection = SortDirection.Desc }, default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Listed);
            Assert.Equal("name", call.Sort[0].Field);
            Assert.Equal(SortDirection.Desc, call.Sort[0].Direction);
            Assert.Equal("id", call.Sort[1].Field);
            Assert.Equal(SortDirection.Desc, call.Sort[1].Direction);
        }

        [Fact]
        public async Task Filters_ArePassedThroughToTheStore()
        {
            var handler = new ListProductsQueryHandler(store, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });
            var query = new ListProductsQuery
            {
                CategoryId = Guid.NewGuid(),
                Kind = ProductKind.Food,
                Caffeine = true,
                Tag = "hot",
                Status = ProductStatus.Inactive,
                Search = "latte",
            };

            var result = await handler.Handle(query, default);

            Assert.True(result.IsSuccess);
            var call = Assert.Single(store.Listed);
            Assert.Equal(query.CategoryId, call.CategoryId);
            Assert.Equal(ProductKind.Food, call.Kind);
            Assert.True(call.Caffeine);
            Assert.Equal("hot", call.Tag);
            Assert.Equal(ProductStatus.Inactive, call.Status);
            Assert.Equal("latte", call.Search);
        }

        [Fact]
        public async Task UnknownSortFieldReturnsAValidationError()
        {
            var handler = new ListProductsQueryHandler(store, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

            var result = await handler.Handle(
                new ListProductsQuery { Sort = "bestselling" }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal("Pagination.SortFieldNotAllowed", result.Error!.Code);
        }
    }
}
