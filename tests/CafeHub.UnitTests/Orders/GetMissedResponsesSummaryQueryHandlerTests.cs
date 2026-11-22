using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Orders;
using CafeHub.Application.Orders.Contracts;
using CafeHub.Application.Orders.Queries;
using CafeHub.Application.Pagination;

namespace CafeHub.UnitTests.Orders
{
    public sealed class GetMissedResponsesSummaryQueryHandlerTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid UserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 11, 30);

        [Fact]
        public async Task StaffAreFixedToTheirOwnBranch()
        {
            var store = new FakeSummaryStore(new MissedResponseSummaryPage([], null));
            var handler = CreateHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(new GetMissedResponsesSummaryQuery { From = From, To = To }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task AForeignBranchReturnsTheUniformNotFound()
        {
            var store = new FakeSummaryStore(new MissedResponseSummaryPage([], null));
            var handler = CreateHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new GetMissedResponsesSummaryQuery { BranchId = OtherBranchId, From = From, To = To }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.NotFoundCode, result.Error!.Code);
            Assert.Null(store.LastRequest);
        }

        [Fact]
        public async Task AnOwnerMustPassABranchId()
        {
            var store = new FakeSummaryStore(new MissedResponseSummaryPage([], null));
            var handler = CreateHandler(store, OwnerUser());

            var result = await handler.Handle(new GetMissedResponsesSummaryQuery { From = From, To = To }, default);

            Assert.False(result.IsSuccess);
            Assert.Equal(OrdersErrors.BranchRequiredCode, result.Error!.Code);
            Assert.Null(store.LastRequest);
        }

        [Fact]
        public async Task AnOwnerReadsThePassedBranch()
        {
            var store = new FakeSummaryStore(new MissedResponseSummaryPage([], null));
            var handler = CreateHandler(store, OwnerUser());

            var result = await handler.Handle(
                new GetMissedResponsesSummaryQuery { BranchId = BranchId, From = From, To = To }, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(BranchId, store.LastRequest!.BranchId);
        }

        [Fact]
        public async Task ThePaginationAndDateRangeAreForwarded()
        {
            var entry = new MissedResponseSummaryEntry(UserId, "Sara", 3);
            var store = new FakeSummaryStore(new MissedResponseSummaryPage(Enumerable.Repeat(entry, 6).ToList(), null));
            var handler = CreateHandler(store, StaffUser(BranchId));

            var result = await handler.Handle(
                new GetMissedResponsesSummaryQuery
                {
                    Page = 2,
                    PageSize = 5,
                    IncludeTotalCount = true,
                    From = From,
                    To = To,
                },
                default);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, store.LastRequest!.Page);
            Assert.Equal(5, store.LastRequest.PageSize);
            Assert.True(store.LastRequest.IncludeTotalCount);
            Assert.Equal(From, store.LastRequest.From);
            Assert.Equal(To, store.LastRequest.To);
            Assert.True(result.Value!.HasNextPage);
            Assert.Equal(5, result.Value.Items.Count);
        }

        private static GetMissedResponsesSummaryQueryHandler CreateHandler(
            FakeSummaryStore store,
            FakeTenantUser user) =>
            new(store, user, new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 });

        private static FakeTenantUser StaffUser(Guid branchId) =>
            new(UserId, new TenantAccountAccess(true, null, true) { BranchId = branchId });

        private static FakeTenantUser OwnerUser() =>
            new(Guid.CreateVersion7(), new TenantAccountAccess(false, null, false));

        private sealed class FakeSummaryStore(MissedResponseSummaryPage page) : IMissedResponseStore
        {
            public MissedResponseSummaryRequest? LastRequest { get; private set; }

            public Task<MissedResponseSummaryPage> ListSummaryAsync(
                MissedResponseSummaryRequest request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(page);
            }
        }

        private sealed class FakeTenantUser(Guid? userId, TenantAccountAccess? accountAccess) : ICurrentTenantUser
        {
            public bool IsAuthenticated => true;

            public Guid? UserId => userId;

            public Guid? TenantId => BranchId;

            public IReadOnlyCollection<string> Roles => [];

            public Guid? BranchId => AccountAccess?.BranchId;

            public TenantAccountAccess? AccountAccess { get; } = accountAccess;
        }
    }
}
