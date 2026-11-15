using CafeHub.Application.Identity;
using CafeHub.Application.Pagination;
using CafeHub.Application.Staff;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Application.Staff.Queries;

namespace CafeHub.UnitTests.Staff.Queries
{
    public sealed class ListStaffQueryHandlerTests
    {
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        private readonly FakeStaffStore store = new();
        private readonly PaginationOptions paginationOptions = new() { DefaultPageSize = 20, MaxPageSize = 100 };
        private readonly ListStaffQueryHandler handler;

        public ListStaffQueryHandlerTests()
        {
            handler = new ListStaffQueryHandler(store, paginationOptions, new FakeTask42Access());
        }

        [Fact]
        public async Task DelegatedListScopesItemsAndCountAndRejectsAnExplicitForeignBranch()
        {
            var foreign = Guid.NewGuid();
            var access = new FakeTask42Access { AccountAccess = new TenantAccountAccess(true, null, true) { BranchId = BranchId } };
            var delegated = new ListStaffQueryHandler(store, paginationOptions, access);
            store.ListResults.Add(FakeStaffStore.MakeResponse(Guid.NewGuid(), "Other Staff", "other@example.test", null, foreign));
            store.ListResults.Add(FakeStaffStore.MakeResponse(Guid.NewGuid(), "Local Staff", "local@example.test", null, BranchId));

            var result = await delegated.Handle(new ListStaffQuery { IncludeTotalCount = true, PageSize = 1 }, default);
            var denied = await delegated.Handle(new ListStaffQuery { BranchId = foreign }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, Assert.Single(result.Value.Items).BranchId);
            Assert.Equal(1, result.Value.TotalCount);
            Assert.Equal(StaffErrors.NotFound(), denied.Error);
            Assert.Equal(BranchId, Assert.Single(store.ListRequests).BranchId);
        }

        [Fact]
        public async Task Handle_WithDefaults_ReturnsEmptyPage()
        {
            var result = await handler.Handle(new ListStaffQuery(), CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(result.Value.Items);
            Assert.Equal(1, result.Value.Page);
            Assert.Equal(20, result.Value.PageSize);
        }

        [Fact]
        public async Task Handle_ReturnsStaffItems_WhenStoreHasData()
        {
            store.ListResults.Add(FakeStaffStore.MakeResponse(Guid.CreateVersion7(), "Staff One", "staff1@test.com", null, BranchId));
            store.ListResults.Add(FakeStaffStore.MakeResponse(Guid.CreateVersion7(), "Staff Two", "staff2@test.com", null, BranchId));

            var result = await handler.Handle(new ListStaffQuery { PageSize = 10 }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Value.Items.Count);
        }

        [Fact]
        public async Task Handle_PassesBranchIdFilterToStore()
        {
            await handler.Handle(new ListStaffQuery { BranchId = BranchId }, CancellationToken.None);

            var request = Assert.Single(store.ListRequests);
            Assert.Equal(BranchId, request.BranchId);
        }

        [Fact]
        public async Task Handle_PassesIsActiveFilterToStore()
        {
            await handler.Handle(new ListStaffQuery { IsActive = false }, CancellationToken.None);

            var request = Assert.Single(store.ListRequests);
            Assert.Equal(false, request.IsActive);
        }

        [Fact]
        public async Task Handle_WithIncludeTotalCount_ReturnsTotalCount()
        {
            store.ListResults.Add(FakeStaffStore.MakeResponse(Guid.CreateVersion7(), "Staff One", "staff1@test.com", null, BranchId));

            var result = await handler.Handle(new ListStaffQuery { IncludeTotalCount = true, PageSize = 10 }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.Value.TotalCount);
        }

        [Fact]
        public async Task Handle_UsesConfiguredDefaultPageSize()
        {
            paginationOptions.DefaultPageSize = 5;

            await handler.Handle(new ListStaffQuery(), CancellationToken.None);

            var request = Assert.Single(store.ListRequests);
            Assert.Equal(5, request.PageSize);
        }

        [Fact]
        public void TheHandler_RequiresEveryDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new ListStaffQueryHandler(null!, paginationOptions, new FakeTask42Access()));
            Assert.Throws<ArgumentNullException>(() => new ListStaffQueryHandler(store, null!, new FakeTask42Access()));
        }
    }
}
