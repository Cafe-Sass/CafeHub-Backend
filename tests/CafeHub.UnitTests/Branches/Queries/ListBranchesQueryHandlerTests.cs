using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Branches.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Branches.Queries
{
    public sealed class ListBranchesQueryHandlerTests
    {
        private readonly FakeBranchStore store = new();
        private readonly PaginationOptions paginationOptions = new() { DefaultPageSize = 20, MaxPageSize = 100 };
        private readonly ListBranchesQueryHandler handler;

        public ListBranchesQueryHandlerTests()
        {
            handler = new ListBranchesQueryHandler(store, paginationOptions);
        }

        [Fact]
        public async Task Handle_WithDefaults_ResolvesTheDocumentedPageSizeAndSort()
        {
            var result = await handler.Handle(new ListBranchesQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var request = Assert.Single(store.ListRequests);
            Assert.Equal(1, request.Page);
            Assert.Equal(20, request.PageSize);
            Assert.False(request.IncludeTotalCount);
            Assert.Equal(
                new[]
                {
                    new SortTerm(BranchListSort.NameField, SortDirection.Asc),
                    new SortTerm(SortWhitelist.IdentifierField, SortDirection.Asc),
                },
                request.Sort);
            Assert.Equal(1, result.Value.Page);
            Assert.Equal(20, result.Value.PageSize);
            Assert.Null(result.Value.TotalCount);
            Assert.False(result.Value.HasNextPage);
            Assert.Empty(result.Value.Items);
        }

        [Fact]
        public async Task Handle_UsesTheConfiguredDefaultPageSize()
        {
            paginationOptions.DefaultPageSize = 5;

            var result = await handler.Handle(new ListBranchesQuery { Page = 3 }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(5, Assert.Single(store.ListRequests).PageSize);
            Assert.Equal(3, result.Value.Page);
        }

        [Fact]
        public async Task Handle_WithTotalCountRequested_ReturnsTheCountAndTrimsTheExtraRow()
        {
            store.ListResponses.Add(Response("branch-one", BranchLifecycleStatus.Active));
            store.ListResponses.Add(Response("branch-two", BranchLifecycleStatus.Active));
            store.ListResponses.Add(Response("branch-three", BranchLifecycleStatus.Suspended));

            var result = await handler.Handle(
                new ListBranchesQuery { Page = 2, PageSize = 2, IncludeTotalCount = true },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value;
            Assert.Equal(2, page.Items.Count);
            Assert.Equal(2, page.Page);
            Assert.Equal(2, page.PageSize);
            Assert.Equal(3, page.TotalCount);
            Assert.True(page.HasNextPage);
            Assert.True(Assert.Single(store.ListRequests).IncludeTotalCount);
        }

        [Fact]
        public async Task Handle_WhenTheFetchedRowsEqualThePageSize_HasNoNextPage()
        {
            store.ListResponses.Add(Response("branch-one", BranchLifecycleStatus.Active));
            store.ListResponses.Add(Response("branch-two", BranchLifecycleStatus.Active));

            var result = await handler.Handle(new ListBranchesQuery { PageSize = 2 }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.Items.Count);
            Assert.False(result.Value.HasNextPage);
            Assert.Null(result.Value.TotalCount);
        }

        [Fact]
        public async Task Handle_WithACreatedAtDescendingSort_ResolvesTheTiebreakerDirection()
        {
            var result = await handler.Handle(
                new ListBranchesQuery { Sort = BranchListSort.CreatedAtField, SortDirection = SortDirection.Desc },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                new[]
                {
                    new SortTerm(BranchListSort.CreatedAtField, SortDirection.Desc),
                    new SortTerm(SortWhitelist.IdentifierField, SortDirection.Desc),
                },
                Assert.Single(store.ListRequests).Sort);
        }

        [Fact]
        public async Task Handle_WithAnUnknownSort_ReturnsThePaginationErrorWithoutQueryingTheStore()
        {
            var result = await handler.Handle(new ListBranchesQuery { Sort = "lifecycleStatus" }, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PaginationErrors.SortFieldNotAllowedCode, result.Error!.Code);
            Assert.Empty(store.ListRequests);
        }

        [Fact]
        public async Task Handle_WithStatusFilter_PassesTheStatusToTheStoreRequest()
        {
            await handler.Handle(new ListBranchesQuery { Status = BranchLifecycleStatus.Suspended }, CancellationToken.None);

            Assert.Equal(BranchLifecycleStatus.Suspended, Assert.Single(store.ListRequests).Status);
        }

        [Fact]
        public async Task Handle_WithANullQuery_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        [Fact]
        public void TheHandler_RequiresEveryDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new ListBranchesQueryHandler(null!, paginationOptions));
            Assert.Throws<ArgumentNullException>(() => new ListBranchesQueryHandler(store, null!));
        }

        [Fact]
        public void TheQuery_IsAQueryOfAnOffsetPageOfTheBranchResponse()
        {
            Assert.True(typeof(IQuery<OffsetPage<BranchResponse>>).IsAssignableFrom(typeof(ListBranchesQuery)));
            Assert.True(
                typeof(IQueryHandler<ListBranchesQuery, OffsetPage<BranchResponse>>)
                    .IsAssignableFrom(typeof(ListBranchesQueryHandler)));
        }

        private static BranchResponse Response(string name, BranchLifecycleStatus status)
        {
            return new BranchResponse
            {
                Id = Guid.CreateVersion7(),
                Name = name,
                LifecycleStatus = status.ToString(),
                Address = null,
                ContactPhone = null,
                OpeningHoursSummary = null,
                CreatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                UpdatedAt = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero),
                ConcurrencyToken = "12345",
            };
        }
    }
}
