using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Plans;
using CafeHub.Application.Plans.DTOs;
using CafeHub.Application.Plans.Queries;
using CafeHub.Domain.Plans;
using CafeHub.UnitTests.Plans.Domain;

namespace CafeHub.UnitTests.Plans.Queries
{
    public sealed class ListPlansQueryHandlerTests
    {
        private readonly FakePlanRepository repository = new();
        private readonly PaginationOptions paginationOptions = new() { DefaultPageSize = 20, MaxPageSize = 100 };
        private readonly ListPlansQueryHandler handler;

        public ListPlansQueryHandlerTests()
        {
            handler = new ListPlansQueryHandler(repository, paginationOptions);
        }

        [Fact]
        public async Task Handle_WithDefaults_ResolvesTheDocumentedPageSizeAndSort()
        {
            var result = await handler.Handle(new ListPlansQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var request = Assert.Single(repository.ListRequests);
            Assert.Equal(1, request.Page);
            Assert.Equal(20, request.PageSize);
            Assert.False(request.IncludeTotalCount);
            Assert.Equal(
                new[]
                {
                    new SortTerm(PlanListSort.KeyField, SortDirection.Asc),
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

            var result = await handler.Handle(new ListPlansQuery { Page = 3 }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(5, Assert.Single(repository.ListRequests).PageSize);
            Assert.Equal(3, result.Value.Page);
        }

        [Fact]
        public async Task Handle_WithTotalCountRequested_ReturnsTheCountAndTrimsTheExtraRow()
        {
            repository.Add(PlanTestData.CreatePlan(code: "bronze"));
            repository.Add(PlanTestData.CreatePlan(code: "gold"));
            repository.Add(PlanTestData.CreatePlan(code: "silver"));
            repository.TotalCount = 7;

            var result = await handler.Handle(
                new ListPlansQuery { Page = 2, PageSize = 2, IncludeTotalCount = true },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            var page = result.Value;
            Assert.Equal(2, page.Items.Count);
            Assert.Equal(2, page.Page);
            Assert.Equal(2, page.PageSize);
            Assert.Equal(7, page.TotalCount);
            Assert.True(page.HasNextPage);
            Assert.True(Assert.Single(repository.ListRequests).IncludeTotalCount);
        }

        [Fact]
        public async Task Handle_WhenTheFetchedRowsEqualThePageSize_HasNoNextPage()
        {
            repository.Add(PlanTestData.CreatePlan(code: "bronze"));
            repository.Add(PlanTestData.CreatePlan(code: "gold"));

            var result = await handler.Handle(new ListPlansQuery { PageSize = 2 }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.Items.Count);
            Assert.False(result.Value.HasNextPage);
            Assert.Null(result.Value.TotalCount);
        }

        [Fact]
        public async Task Handle_WithACreatedAtDescendingSort_ResolvesTheTiebreakerDirection()
        {
            var result = await handler.Handle(
                new ListPlansQuery { Sort = PlanListSort.CreatedAtField, SortDirection = SortDirection.Desc },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                new[]
                {
                    new SortTerm(PlanListSort.CreatedAtField, SortDirection.Desc),
                    new SortTerm(SortWhitelist.IdentifierField, SortDirection.Desc),
                },
                Assert.Single(repository.ListRequests).Sort);
        }

        [Fact]
        public async Task Handle_WithAnUnknownSort_ReturnsThePaginationErrorWithoutQueryingTheRepository()
        {
            var result = await handler.Handle(new ListPlansQuery { Sort = "name" }, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(PaginationErrors.SortFieldNotAllowedCode, result.Error!.Code);
            Assert.Empty(repository.ListRequests);
        }

        [Fact]
        public async Task Handle_MapsVersionsNewestFirstAndEntitlementsByKey()
        {
            var plan = PlanTestData.CreatePlan();
            var firstVersionId = Guid.NewGuid();
            PlanTestData.AddVersion(
                plan,
                versionId: firstVersionId,
                entitlements: new[]
                {
                    PlanTestData.CreateEntitlement(firstVersionId, key: "z_key", value: "1"),
                    PlanTestData.CreateEntitlement(firstVersionId, key: "a_key", value: "2"),
                });
            PlanTestData.AddVersion(plan);
            repository.Add(plan);

            var result = await handler.Handle(new ListPlansQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            var response = Assert.Single(result.Value.Items);
            Assert.Equal(new[] { 2, 1 }, response.Versions.Select(version => version.Number));
            Assert.Equal(new[] { "a_key", "z_key" }, response.Versions[1].Entitlements.Select(entitlement => entitlement.Key));
        }

        [Fact]
        public async Task Handle_WithANullQuery_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => handler.Handle(null!, CancellationToken.None));
        }

        [Fact]
        public void TheHandler_RequiresEveryDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new ListPlansQueryHandler(null!, paginationOptions));
            Assert.Throws<ArgumentNullException>(() => new ListPlansQueryHandler(repository, null!));
        }

        [Fact]
        public void TheQuery_IsAQueryOfAnOffsetPageOfThePlanResponse()
        {
            Assert.True(typeof(IQuery<OffsetPage<PlanResponse>>).IsAssignableFrom(typeof(ListPlansQuery)));
            Assert.True(
                typeof(IQueryHandler<ListPlansQuery, OffsetPage<PlanResponse>>)
                    .IsAssignableFrom(typeof(ListPlansQueryHandler)));
        }
    }
}
