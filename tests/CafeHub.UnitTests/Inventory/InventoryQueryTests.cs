using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Inventory.Queries;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryQueryTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 12, 31);
        private static readonly InventoryItemResponse Item =
            new(Guid.NewGuid(), "Coffee Beans", "Kg", 5m, true, 7, "Active", "1");
        private static readonly InventoryBatchResponse Batch = new(
            Guid.NewGuid(), Guid.NewGuid(), BranchId, 2.5m, 2.5m, From, To, Guid.NewGuid());

        [Fact]
        public void ListItemsValidator_EnforcesPagingAndADefinedStatus()
        {
            var validator = new ListInventoryItemsQueryValidator(new PaginationOptions());

            Assert.True(validator.Validate(new ListInventoryItemsQuery()).IsValid);
            Assert.True(validator.Validate(new ListInventoryItemsQuery
            {
                Status = InventoryItemStatus.Inactive,
                Page = 2,
                PageSize = 100,
                IncludeTotalCount = true,
            }).IsValid);
            Assert.False(validator.Validate(new ListInventoryItemsQuery { Page = 0 }).IsValid);
            Assert.False(validator.Validate(new ListInventoryItemsQuery { PageSize = 0 }).IsValid);
            Assert.False(validator.Validate(new ListInventoryItemsQuery { PageSize = 101 }).IsValid);

            var undefined = validator.Validate(new ListInventoryItemsQuery { Status = (InventoryItemStatus)99 });
            Assert.False(undefined.IsValid);
            Assert.Equal(InventoryItemErrors.InvalidStatusCode, undefined.Errors[0].ErrorCode);
        }

        [Fact]
        public async Task ListItemsHandler_ResolvesThePageSizeAndForwardsTheStatusFilter()
        {
            var store = new FakeItemStore { Page = new InventoryItemListPage(new[] { Item }, 1) };
            var handler = new ListInventoryItemsQueryHandler(
                store, new PaginationOptions { DefaultPageSize = 25, MaxPageSize = 100 });

            var result = await handler.Handle(
                new ListInventoryItemsQuery { Status = InventoryItemStatus.Inactive }, default);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Value.Items);
            Assert.Equal(25, result.Value.PageSize);
            Assert.Equal(1, result.Value.TotalCount);
            Assert.False(result.Value.HasNextPage);
            var request = Assert.Single(store.Requests);
            Assert.Equal(InventoryItemStatus.Inactive, request.Status);
            Assert.Equal(25, request.PageSize);
            Assert.Equal(1, request.Page);
            Assert.False(request.IncludeTotalCount);

            await handler.Handle(new ListInventoryItemsQuery { PageSize = 10, IncludeTotalCount = true }, default);

            Assert.Equal(10, store.Requests[1].PageSize);
            Assert.True(store.Requests[1].IncludeTotalCount);
        }

        [Fact]
        public async Task ListItemsHandler_DerivesHasNextPageFromTheExtraRow()
        {
            var store = new FakeItemStore
            {
                Page = new InventoryItemListPage(new[] { Item, Item, Item }, null),
            };
            var handler = new ListInventoryItemsQueryHandler(store, new PaginationOptions());

            var result = await handler.Handle(new ListInventoryItemsQuery { PageSize = 2 }, default);

            Assert.Equal(2, result.Value.Items.Count);
            Assert.True(result.Value.HasNextPage);
            Assert.Null(result.Value.TotalCount);
        }

        [Fact]
        public async Task GetItemHandler_ReturnsTheItemOrTheUniformNotFound()
        {
            var found = await new GetInventoryItemQueryHandler(new FakeItemStore { Item = Item })
                .Handle(new GetInventoryItemQuery(Item.Id), default);
            Assert.Equal(Item, found.Value);

            var missing = await new GetInventoryItemQueryHandler(new FakeItemStore())
                .Handle(new GetInventoryItemQuery(Guid.NewGuid()), default);

            Assert.Equal(InventoryItemErrors.NotFound(), missing.Error);
        }

        [Fact]
        public void ListBatchesValidator_EnforcesBranchPagingAndTheExpiryRange()
        {
            var validator = new ListInventoryBatchesQueryValidator(new PaginationOptions());
            var valid = new ListInventoryBatchesQuery { BranchId = BranchId, ExpiryFrom = From, ExpiryTo = To };

            Assert.True(validator.Validate(valid).IsValid);
            Assert.True(validator.Validate(valid with { ExpiryTo = null }).IsValid);
            Assert.False(validator.Validate(valid with { BranchId = Guid.Empty }).IsValid);
            Assert.False(validator.Validate(valid with { ItemId = Guid.Empty }).IsValid);
            Assert.False(validator.Validate(valid with { Page = 0 }).IsValid);
            Assert.False(validator.Validate(valid with { PageSize = 101 }).IsValid);

            var reversed = validator.Validate(valid with { ExpiryFrom = To, ExpiryTo = From });
            Assert.False(reversed.IsValid);
            Assert.Equal(InventoryBatchErrors.InvalidExpiryRangeCode, reversed.Errors[0].ErrorCode);
        }

        [Fact]
        public async Task ListBatchesHandler_RefusesAForeignBranchWithTheUniformNotFound()
        {
            var store = new FakeBatchStore();
            var handler = new ListInventoryBatchesQueryHandler(
                store, StaffUser(OtherBranchId), new PaginationOptions());

            var result = await handler.Handle(
                new ListInventoryBatchesQuery { BranchId = BranchId }, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), result.Error);
            Assert.Empty(store.Requests);
        }

        [Fact]
        public async Task ListBatchesHandler_ForwardsTheFiltersAndBuildsThePage()
        {
            var store = new FakeBatchStore
            {
                Page = Result.Success(new InventoryBatchListPage(new[] { Batch }, 1)),
            };
            var handler = new ListInventoryBatchesQueryHandler(
                store, StaffUser(BranchId), new PaginationOptions { DefaultPageSize = 15 });

            var result = await handler.Handle(
                new ListInventoryBatchesQuery
                {
                    BranchId = BranchId,
                    ItemId = Batch.ItemId,
                    ExpiryFrom = From,
                    ExpiryTo = To,
                },
                default);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Value.Items);
            Assert.Equal(15, result.Value.PageSize);
            Assert.Equal(1, result.Value.TotalCount);
            var request = Assert.Single(store.Requests);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(Batch.ItemId, request.ItemId);
            Assert.Equal(From, request.ExpiryFrom);
            Assert.Equal(To, request.ExpiryTo);
            Assert.Equal(15, request.PageSize);
        }

        [Fact]
        public async Task ListBatchesHandler_PropagatesAStoreFailure()
        {
            var store = new FakeBatchStore
            {
                Page = Result.Failure<InventoryBatchListPage>(InventoryBatchErrors.BranchNotFound()),
            };
            var handler = new ListInventoryBatchesQueryHandler(store, Owner(), new PaginationOptions());

            var result = await handler.Handle(
                new ListInventoryBatchesQuery { BranchId = BranchId }, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), result.Error);
            Assert.Single(store.Requests);
        }

        private static FakeTenantUser Owner() => new();

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, "1", true) { BranchId = branchId },
        };

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeItemStore : IInventoryItemStore
        {
            public List<InventoryItemListRequest> Requests { get; } = new();

            public InventoryItemListPage Page { get; init; } = new(Array.Empty<InventoryItemResponse>(), null);

            public InventoryItemResponse? Item { get; init; }

            public Task<Result<InventoryItemResponse>> CreateAsync(
                string name,
                InventoryUnit unit,
                decimal lowStockThreshold,
                bool tracksExpiry,
                int nearExpiryDays,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The query tests do not create items.");

            public Task<Result<InventoryItemResponse>> UpdateAsync(
                Guid id,
                string name,
                InventoryUnit unit,
                decimal lowStockThreshold,
                bool tracksExpiry,
                int nearExpiryDays,
                uint version,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The query tests do not update items.");

            public Task<Result<InventoryItemResponse>> ChangeStatusAsync(
                Guid id,
                InventoryItemStatus status,
                uint version,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The query tests do not change item status.");

            public Task<InventoryItemResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                Task.FromResult(Item);

            public Task<InventoryItemListPage> ListAsync(
                InventoryItemListRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Page);
            }
        }

        private sealed class FakeBatchStore : IInventoryBatchStore
        {
            public List<InventoryBatchListRequest> Requests { get; } = new();

            public Result<InventoryBatchListPage> Page { get; init; } =
                Result.Success(new InventoryBatchListPage(Array.Empty<InventoryBatchResponse>(), null));

            public Task<Result<InventoryBatchResponse>> ReceiveAsync(
                Guid branchId,
                Guid itemId,
                decimal receivedQuantity,
                DateOnly receivedDate,
                DateOnly? expiryDate,
                string idempotencyKey,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The query tests do not receive stock.");

            public Task<Result<InventoryBatchListPage>> ListAsync(
                InventoryBatchListRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Page);
            }

            public Task<InventoryStockLevelPage> ListStockLevelsAsync(
                InventoryStockLevelRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The item query tests do not read stock levels.");
        }
    }
}
