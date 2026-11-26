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
    public sealed class StockLevelAndMovementQueryTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid ActorId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly DateOnly From = new(2026, 11, 1);
        private static readonly DateOnly To = new(2026, 11, 30);
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

        private static readonly StockLevelResponse StockLevel =
            new(ItemId, BranchId, 3m, new DateOnly(2026, 12, 1));

        private static readonly StockMovementResponse Movement =
            new(Guid.NewGuid(), ItemId, BranchId, BatchId, "Consume", -1m, "usage", Now, ActorId);

        [Fact]
        public void StockLevelsValidator_EnforcesPagingAndNonEmptyFilters()
        {
            var validator = new ListStockLevelsQueryValidator(new PaginationOptions());

            Assert.True(validator.Validate(new ListStockLevelsQuery()).IsValid);
            Assert.True(validator.Validate(new ListStockLevelsQuery
            {
                BranchId = BranchId,
                ItemId = ItemId,
                Page = 2,
                PageSize = 100,
                IncludeTotalCount = true,
            }).IsValid);
            Assert.False(validator.Validate(new ListStockLevelsQuery { Page = 0 }).IsValid);
            Assert.False(validator.Validate(new ListStockLevelsQuery { PageSize = 0 }).IsValid);
            Assert.False(validator.Validate(new ListStockLevelsQuery { PageSize = 101 }).IsValid);
            Assert.False(validator.Validate(new ListStockLevelsQuery { BranchId = Guid.Empty }).IsValid);
            Assert.False(validator.Validate(new ListStockLevelsQuery { ItemId = Guid.Empty }).IsValid);
        }

        [Fact]
        public async Task StockLevelsHandler_ForStaffPinsTheBranchAndForwardsTheItemAndPageSize()
        {
            var store = new FakeBatchStore
            {
                Page = new InventoryStockLevelPage(new[] { StockLevel }, 1),
            };
            var handler = new ListStockLevelsQueryHandler(
                store,
                StaffUser(BranchId),
                new PaginationOptions { DefaultPageSize = 15, MaxPageSize = 100 });

            var result = await handler.Handle(
                new ListStockLevelsQuery { ItemId = ItemId, IncludeTotalCount = true },
                default);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Value.Items);
            Assert.Equal(15, result.Value.PageSize);
            Assert.Equal(1, result.Value.TotalCount);
            var request = Assert.Single(store.Requests);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(ItemId, request.ItemId);
            Assert.Equal(15, request.PageSize);
            Assert.True(request.IncludeTotalCount);
        }

        [Fact]
        public async Task StockLevelsHandler_RefusesAForeignBranchForStaff()
        {
            var store = new FakeBatchStore();
            var handler = new ListStockLevelsQueryHandler(
                store, StaffUser(OtherBranchId), new PaginationOptions());

            var result = await handler.Handle(
                new ListStockLevelsQuery { BranchId = BranchId }, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), result.Error);
            Assert.Empty(store.Requests);
        }

        [Fact]
        public async Task StockLevelsHandler_ForTheOwnerKeepsTheOptionalBranchFilter()
        {
            var store = new FakeBatchStore
            {
                Page = new InventoryStockLevelPage(new[] { StockLevel }, null),
            };
            var handler = new ListStockLevelsQueryHandler(store, Owner(), new PaginationOptions());

            var result = await handler.Handle(
                new ListStockLevelsQuery { BranchId = BranchId }, default);

            Assert.True(result.IsSuccess);
            var request = Assert.Single(store.Requests);
            Assert.Equal(BranchId, request.BranchId);
        }

        [Fact]
        public void MovementsValidator_EnforcesPagingTypeAndDateRange()
        {
            var validator = new ListStockMovementsQueryValidator(new PaginationOptions());
            var valid = new ListStockMovementsQuery
            {
                BranchId = BranchId,
                ItemId = ItemId,
                Type = StockMovementType.Waste,
                CreatedFrom = From,
                CreatedTo = To,
            };

            Assert.True(validator.Validate(valid).IsValid);
            Assert.True(validator.Validate(valid with { CreatedTo = null }).IsValid);
            Assert.False(validator.Validate(valid with { Page = 0 }).IsValid);
            Assert.False(validator.Validate(valid with { PageSize = 101 }).IsValid);
            Assert.False(validator.Validate(valid with { BranchId = Guid.Empty }).IsValid);
            Assert.False(validator.Validate(valid with { ItemId = Guid.Empty }).IsValid);

            var undefinedType = validator.Validate(valid with { Type = (StockMovementType)99 });
            Assert.False(undefinedType.IsValid);
            Assert.Equal(StockMovementErrors.InvalidTypeFilterCode, undefinedType.Errors[0].ErrorCode);

            var reversed = validator.Validate(valid with { CreatedFrom = To, CreatedTo = From });
            Assert.False(reversed.IsValid);
            Assert.Equal(StockMovementErrors.InvalidDateRangeCode, reversed.Errors[0].ErrorCode);
        }

        [Fact]
        public async Task MovementsHandler_ForStaffPinsTheBranchAndForwardsTheFilters()
        {
            var store = new FakeMovementStore
            {
                Page = new StockMovementListPage(new[] { Movement }, 1),
            };
            var handler = new ListStockMovementsQueryHandler(
                store,
                StaffUser(BranchId),
                new PaginationOptions { DefaultPageSize = 10 });

            var result = await handler.Handle(
                new ListStockMovementsQuery
                {
                    ItemId = ItemId,
                    Type = StockMovementType.Consume,
                    CreatedFrom = From,
                    CreatedTo = To,
                },
                default);

            Assert.True(result.IsSuccess);
            Assert.Single(result.Value.Items);
            Assert.Equal(10, result.Value.PageSize);
            var request = Assert.Single(store.Requests);
            Assert.Equal(BranchId, request.BranchId);
            Assert.Equal(ItemId, request.ItemId);
            Assert.Equal(StockMovementType.Consume, request.Type);
            Assert.Equal(From, request.CreatedFrom);
            Assert.Equal(To, request.CreatedTo);
            Assert.Equal(10, request.PageSize);
        }

        [Fact]
        public async Task MovementsHandler_ForTheOwnerKeepsTheBranchFilterAndRefusesAForeignBranchForStaff()
        {
            var ownerStore = new FakeMovementStore
            {
                Page = new StockMovementListPage(Array.Empty<StockMovementResponse>(), null),
            };
            var owner = await new ListStockMovementsQueryHandler(ownerStore, Owner(), new PaginationOptions())
                .Handle(new ListStockMovementsQuery { BranchId = BranchId }, default);

            Assert.True(owner.IsSuccess);
            Assert.Equal(BranchId, Assert.Single(ownerStore.Requests).BranchId);

            var staffStore = new FakeMovementStore();
            var staff = await new ListStockMovementsQueryHandler(
                    staffStore, StaffUser(OtherBranchId), new PaginationOptions())
                .Handle(new ListStockMovementsQuery { BranchId = BranchId }, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), staff.Error);
            Assert.Empty(staffStore.Requests);
        }

        [Fact]
        public void Reconciliation_BatchRemainingEqualsReceivesPlusAdjustmentsMinusConsumeAndWaste()
        {
            var today = new DateOnly(2026, 11, 25);
            var first = InventoryBatch.Create(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ItemId,
                BranchId,
                5m,
                today.AddDays(-20),
                null,
                false,
                ActorId,
                "receive-key-1",
                Now,
                ActorId);
            var second = InventoryBatch.Create(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ItemId,
                BranchId,
                3m,
                today.AddDays(-10),
                null,
                false,
                ActorId,
                "receive-key-2",
                Now,
                ActorId);
            var movements = new List<StockMovement>
            {
                ReceiveMovement(first, 5m, "receive-key-1"),
                ReceiveMovement(second, 3m, "receive-key-2"),
            };

            Assert.True(StockAllocation.TryAllocate(
                new[] { first, second }, 6m, today, allowExpired: false, out var allocations));
            Assert.Equal(new[] { first.Id, second.Id }, allocations.Select(line => line.BatchId));
            foreach (var allocation in allocations)
            {
                var batch = allocation.BatchId == first.Id ? first : second;
                batch.ApplyMovement(-allocation.Quantity, Now, ActorId);
                movements.Add(StockMovement.Create(
                    Guid.CreateVersion7(),
                    ItemId,
                    BranchId,
                    batch.Id,
                    StockMovementType.Consume,
                    -allocation.Quantity,
                    "usage",
                    "movement-key-1",
                    Now,
                    ActorId));
            }

            second.ApplyMovement(-1m, Now, ActorId);
            movements.Add(StockMovement.Create(
                Guid.CreateVersion7(),
                ItemId,
                BranchId,
                second.Id,
                StockMovementType.Waste,
                -1m,
                "spoiled",
                "movement-key-2",
                Now,
                ActorId));
            second.ApplyMovement(2m, Now, ActorId);
            movements.Add(StockMovement.Create(
                Guid.CreateVersion7(),
                ItemId,
                BranchId,
                second.Id,
                StockMovementType.Adjust,
                2m,
                "count correction",
                "movement-key-3",
                Now,
                ActorId));

            var remaining = first.RemainingQuantity + second.RemainingQuantity;
            var received = movements.Where(movement => movement.Type == StockMovementType.Receive)
                .Sum(movement => movement.Quantity);
            var adjusted = movements.Where(movement => movement.Type == StockMovementType.Adjust)
                .Sum(movement => movement.Quantity);
            var consumed = -movements.Where(movement => movement.Type == StockMovementType.Consume)
                .Sum(movement => movement.Quantity);
            var wasted = -movements.Where(movement => movement.Type == StockMovementType.Waste)
                .Sum(movement => movement.Quantity);

            Assert.Equal(3m, remaining);
            Assert.Equal(remaining, movements.Sum(movement => movement.Quantity));
            Assert.Equal(received + adjusted - consumed - wasted, remaining);
            Assert.Equal(6m, consumed);
            Assert.Equal(1m, wasted);
            Assert.Throws<ArgumentOutOfRangeException>(() => second.ApplyMovement(-4m, Now, ActorId));
            Assert.Equal(3m, second.RemainingQuantity);
        }

        private static StockMovement ReceiveMovement(InventoryBatch batch, decimal quantity, string key) =>
            StockMovement.Create(
                Guid.CreateVersion7(),
                batch.ItemId,
                batch.BranchId,
                batch.Id,
                StockMovementType.Receive,
                quantity,
                null,
                key,
                Now,
                ActorId);

        private static FakeTenantUser Owner() => new();

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, "1", true) { BranchId = branchId },
        };

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = ActorId;

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeBatchStore : IInventoryBatchStore
        {
            public List<InventoryStockLevelRequest> Requests { get; } = new();

            public InventoryStockLevelPage Page { get; init; } =
                new(Array.Empty<StockLevelResponse>(), null);

            public Task<Result<InventoryBatchResponse>> ReceiveAsync(
                Guid branchId,
                Guid itemId,
                decimal receivedQuantity,
                DateOnly receivedDate,
                DateOnly? expiryDate,
                string idempotencyKey,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The stock query tests do not receive stock.");

            public Task<Result<InventoryBatchListPage>> ListAsync(
                InventoryBatchListRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The stock query tests do not list batches.");

            public Task<InventoryStockLevelPage> ListStockLevelsAsync(
                InventoryStockLevelRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Page);
            }
        }

        private sealed class FakeMovementStore : IStockMovementStore
        {
            public List<StockMovementListRequest> Requests { get; } = new();

            public StockMovementListPage Page { get; init; } =
                new(Array.Empty<StockMovementResponse>(), null);

            public Task<Result<IReadOnlyList<StockMovementResponse>>> RecordAsync(
                Guid branchId,
                Guid itemId,
                Guid? batchId,
                StockMovementType type,
                decimal quantity,
                string reason,
                string idempotencyKey,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException("The history query tests do not record movements.");

            public Task<StockMovementListPage> ListAsync(
                StockMovementListRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                return Task.FromResult(Page);
            }
        }
    }
}
