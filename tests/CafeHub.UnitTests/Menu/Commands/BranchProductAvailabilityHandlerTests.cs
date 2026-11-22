using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Commands;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Menu.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Menu.Commands
{
    public sealed class BranchProductAvailabilityHandlerTests
    {
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ProductId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        private readonly FakeBranchProductAvailabilityStore store = new();
        private readonly StubTenantContextAccessor tenantContext = new(
            TenantContext.Create(TenantId, "tenant-secret-reference", true, "cafe-one"));

        [Fact]
        public async Task Set_ForwardsTheBranchProductAvailabilityAndActor()
        {
            var response = new BranchProductAvailabilityResponse(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, DateTimeOffset.UnixEpoch);
            store.SetResponse = Result.Success(response);
            var actor = Owner();

            var result = await new SetBranchProductAvailabilityCommandHandler(store, actor, tenantContext)
                .Handle(new SetBranchProductAvailabilityCommand(BranchId, ProductId, false, "manual"), default);

            Assert.Same(response, result.Value);
            var call = Assert.Single(store.SetCalls);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal(ProductId, call.ProductId);
            Assert.False(call.Available);
            Assert.Equal("manual", call.Reason);
            Assert.Equal(ActorId, call.ActorId);
        }

        [Fact]
        public async Task List_ForwardsTheBranchFiltersAndResolvedPageSize()
        {
            var page = OffsetPage<BranchProductAvailabilityItem>.Create([], 2, 50, 0);
            store.ListResponse = Result.Success(page);
            var categoryId = Guid.NewGuid();
            var options = new PaginationOptions { DefaultPageSize = 20, MaxPageSize = 100 };

            var result = await new ListBranchProductAvailabilityQueryHandler(store, Owner(), tenantContext, options)
                .Handle(new ListBranchProductAvailabilityQuery
                {
                    BranchId = BranchId,
                    Available = false,
                    CategoryId = categoryId,
                    Page = 2,
                    PageSize = 50,
                    IncludeTotalCount = true,
                }, default);

            Assert.Same(page, result.Value);
            var call = Assert.Single(store.ListCalls);
            Assert.Equal(BranchId, call.BranchId);
            Assert.False(call.Available);
            Assert.Equal(categoryId, call.CategoryId);
            Assert.Equal(2, call.Page);
            Assert.Equal(50, call.PageSize);
            Assert.True(call.IncludeTotalCount);
        }

        [Theory]
        [InlineData("other-tenant")]
        [InlineData("other-branch")]
        [InlineData("inactive-branch")]
        public async Task RejectsOutOfScopeReadsAndWritesBeforeAccessingTenantData(string scope)
        {
            var actor = new FakeTask42Access
            {
                TenantId = scope == "other-tenant" ? Guid.NewGuid() : TenantId,
                UserId = ActorId,
                AccountAccess = new TenantAccountAccess(true, "fixture", scope != "inactive-branch")
                {
                    BranchId = scope == "other-branch" ? Guid.NewGuid() : BranchId,
                },
            };
            var unreachable = new UnreachableStore();
            var read = await new ListBranchProductAvailabilityQueryHandler(
                    unreachable, actor, tenantContext, new PaginationOptions())
                .Handle(new ListBranchProductAvailabilityQuery { BranchId = BranchId }, default);
            var write = await new SetBranchProductAvailabilityCommandHandler(unreachable, actor, tenantContext)
                .Handle(new SetBranchProductAvailabilityCommand(BranchId, ProductId, false, "manual"), default);

            var expected = scope == "other-branch" ? ProductErrors.NotFound() : AuthErrors.Forbidden();
            Assert.Equal(expected, read.Error);
            Assert.Equal(expected, write.Error);
        }

        [Fact]
        public async Task StaffCanChangeAvailabilityInTheirOwnBranch()
        {
            store.SetResponse = Result.Success(new BranchProductAvailabilityResponse(
                BranchId, ProductId, false, BranchProductAvailabilityReasons.Manual, DateTimeOffset.UnixEpoch));
            var actor = new FakeTask42Access
            {
                TenantId = TenantId,
                UserId = ActorId,
                AccountAccess = new TenantAccountAccess(true, "fixture", true) { BranchId = BranchId },
            };

            var result = await new SetBranchProductAvailabilityCommandHandler(store, actor, tenantContext)
                .Handle(new SetBranchProductAvailabilityCommand(BranchId, ProductId, false, "manual"), default);

            Assert.True(result.IsSuccess);
            Assert.Single(store.SetCalls);
        }

        private static FakeTask42Access Owner() => new()
        {
            TenantId = TenantId,
            UserId = ActorId,
            AccountAccess = new TenantAccountAccess(false, null, false),
        };

        private sealed class FakeBranchProductAvailabilityStore : IBranchProductAvailabilityStore
        {
            public List<SetAvailabilityCall> SetCalls { get; } = new();
            public List<ListAvailabilityCall> ListCalls { get; } = new();
            public Result<BranchProductAvailabilityResponse>? SetResponse { get; set; }
            public Result<OffsetPage<BranchProductAvailabilityItem>>? ListResponse { get; set; }

            public Task<Result<BranchProductAvailabilityResponse>> SetAsync(
                Guid branchId, Guid productId, bool available, string reason, Guid actorId,
                CancellationToken cancellationToken)
            {
                SetCalls.Add(new SetAvailabilityCall(branchId, productId, available, reason, actorId));
                return Task.FromResult(SetResponse!);
            }

            public Task<Result<OffsetPage<BranchProductAvailabilityItem>>> ListAsync(
                Guid branchId, bool? available, Guid? categoryId, int page, int pageSize, bool includeTotalCount,
                CancellationToken cancellationToken)
            {
                ListCalls.Add(new ListAvailabilityCall(
                    branchId, available, categoryId, page, pageSize, includeTotalCount));
                return Task.FromResult(ListResponse!);
            }
        }

        private sealed class UnreachableStore : IBranchProductAvailabilityStore
        {
            public Task<Result<BranchProductAvailabilityResponse>> SetAsync(
                Guid branchId, Guid productId, bool available, string reason, Guid actorId,
                CancellationToken cancellationToken) => throw new NotSupportedException();

            public Task<Result<OffsetPage<BranchProductAvailabilityItem>>> ListAsync(
                Guid branchId, bool? available, Guid? categoryId, int page, int pageSize, bool includeTotalCount,
                CancellationToken cancellationToken) => throw new NotSupportedException();
        }

        private sealed record SetAvailabilityCall(
            Guid BranchId, Guid ProductId, bool Available, string Reason, Guid ActorId);

        private sealed record ListAvailabilityCall(
            Guid BranchId, bool? Available, Guid? CategoryId, int Page, int PageSize, bool IncludeTotalCount);
    }
}
