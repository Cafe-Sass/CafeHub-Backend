using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class RecordStockMovementCommandTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        private static readonly StockMovementResponse Response = new(
            Guid.NewGuid(), ItemId, BranchId, BatchId, "Consume", -2.5m, "spillage", Now, Guid.NewGuid());

        [Fact]
        public void Validator_AcceptsEachManualTypeAndRefusesFieldViolations()
        {
            var validator = new RecordStockMovementCommandValidator();

            Assert.True(validator.Validate(Command()).IsValid);
            Assert.True(validator.Validate(Command(type: StockMovementType.Waste)).IsValid);
            Assert.True(validator.Validate(Command(
                type: StockMovementType.Adjust, quantity: -1m, batchId: BatchId, reason: "count correction")).IsValid);
            Assert.True(validator.Validate(Command(
                type: StockMovementType.Adjust, quantity: 1m, batchId: BatchId, reason: "count correction")).IsValid);

            Assert.False(validator.Validate(Command(type: StockMovementType.Receive)).IsValid);
            Assert.False(validator.Validate(Command(reason: null)).IsValid);
            Assert.False(validator.Validate(Command(reason: " ")).IsValid);
            Assert.False(validator.Validate(Command(quantity: 0m)).IsValid);
            Assert.False(validator.Validate(Command(quantity: -1m)).IsValid);
            Assert.False(validator.Validate(Command(quantity: 0.0001m)).IsValid);
            Assert.False(validator.Validate(Command(quantity: 1_000_000_000m)).IsValid);
            Assert.False(validator.Validate(Command(branchId: Guid.Empty)).IsValid);
            Assert.False(validator.Validate(Command(itemId: Guid.Empty)).IsValid);
            Assert.False(validator.Validate(Command(batchId: BatchId)).IsValid);
            Assert.False(validator.Validate(Command(type: StockMovementType.Adjust, quantity: -1m)).IsValid);
            Assert.False(validator.Validate(Command(
                type: StockMovementType.Adjust, quantity: 0m, batchId: BatchId)).IsValid);
            Assert.False(validator.Validate(Command(idempotencyKey: null)).IsValid);
            Assert.False(validator.Validate(Command(idempotencyKey: "short")).IsValid);
            Assert.False(validator.Validate(Command(idempotencyKey: "movement key 1")).IsValid);
        }

        [Fact]
        public async Task Handler_ForwardsTheMovementAndTheAuthenticatedActor()
        {
            var store = new FakeStore();
            var handler = new RecordStockMovementCommandHandler(store, User(), Context());

            var result = await handler.Handle(Command(reason: "  spillage  "), default);

            Assert.Equal(new[] { Response }, result.Value);
            var call = Assert.IsType<RecordCall>(store.Recorded);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal(ItemId, call.ItemId);
            Assert.Null(call.BatchId);
            Assert.Equal(StockMovementType.Consume, call.Type);
            Assert.Equal(2.5m, call.Quantity);
            Assert.Equal("spillage", call.Reason);
            Assert.Equal("movement-key-1", call.IdempotencyKey);
            Assert.Equal(User().UserId!.Value, call.ActorUserId);
        }

        [Fact]
        public async Task Handler_ForAStaffMemberOfAnotherBranchReturnsTheUniformNotFound()
        {
            var store = new FakeStore();
            var handler = new RecordStockMovementCommandHandler(store, StaffUser(BranchId), Context());

            var result = await handler.Handle(Command(branchId: OtherBranchId), default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), result.Error);
            Assert.Null(store.Recorded);
        }

        [Fact]
        public async Task Handler_AllowsTheStaffMembersOwnBranch()
        {
            var store = new FakeStore();
            var handler = new RecordStockMovementCommandHandler(store, StaffUser(BranchId), Context());

            var result = await handler.Handle(Command(), default);

            Assert.True(result.IsSuccess);
            Assert.NotNull(store.Recorded);
        }

        [Fact]
        public async Task Handler_RefusesAStaffMemberWithoutAnActiveBranch()
        {
            var store = new FakeStore();
            var user = new FakeTenantUser
            {
                AccountAccess = new TenantAccountAccess(true, "1", false),
            };
            var handler = new RecordStockMovementCommandHandler(store, user, Context());

            var result = await handler.Handle(Command(), default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Null(store.Recorded);
        }

        [Fact]
        public async Task Handler_RefusesAMissingTenantActorWithTheUniformAuthError()
        {
            var store = new FakeStore();
            var anonymous = new FakeTenantUser { UserId = Guid.Empty };
            var handler = new RecordStockMovementCommandHandler(store, anonymous, Context());

            var result = await handler.Handle(Command(), default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailable(), result.Error);
            Assert.Null(store.Recorded);
        }

        [Fact]
        public void Permission_CatalogMakesInventoryMoveAStaffGrant()
        {
            Assert.Contains(TenantPermissions.InventoryMove, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.InventoryMove));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.InventoryMove));
            Assert.Equal("inventory.move", TenantPermissions.InventoryMove);
        }

        private static RecordStockMovementCommand Command(
            Guid? branchId = null,
            Guid? itemId = null,
            Guid? batchId = null,
            StockMovementType type = StockMovementType.Consume,
            decimal quantity = 2.5m,
            string? reason = "spillage",
            string? idempotencyKey = "movement-key-1") => new()
            {
                BranchId = branchId ?? BranchId,
                ItemId = itemId ?? ItemId,
                BatchId = batchId,
                Type = type,
                Quantity = quantity,
                Reason = reason,
                IdempotencyKey = idempotencyKey,
            };

        private static FakeTenantUser User() => new();

        private static FakeTenantUser StaffUser(Guid branchId) => new()
        {
            BranchId = branchId,
            AccountAccess = new TenantAccountAccess(true, "1", true) { BranchId = branchId },
        };

        private static StubTenantContextAccessor Context() =>
            new(TenantContext.Create(FixtureTenantId, "fixture", true));

        private sealed class FakeTenantUser : ICurrentTenantUser
        {
            public bool IsAuthenticated { get; init; } = true;

            public Guid? UserId { get; init; } = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

            public Guid? TenantId { get; init; } = FixtureTenantId;

            public IReadOnlyCollection<string> Roles { get; init; } = new[] { "Owner" };

            public Guid? BranchId { get; init; }

            public TenantAccountAccess? AccountAccess { get; init; } = new(false, "1", false);
        }

        private sealed class FakeStore : IStockMovementStore
        {
            public RecordCall? Recorded { get; private set; }

            public Task<Result<IReadOnlyList<StockMovementResponse>>> RecordAsync(
                Guid branchId,
                Guid itemId,
                Guid? batchId,
                StockMovementType type,
                decimal quantity,
                string reason,
                string idempotencyKey,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Recorded = new RecordCall(
                    branchId, itemId, batchId, type, quantity, reason, idempotencyKey, actorUserId);
                return Task.FromResult(Result.Success<IReadOnlyList<StockMovementResponse>>(
                    new[] { Response }));
            }
        }

        private sealed record RecordCall(
            Guid BranchId,
            Guid ItemId,
            Guid? BatchId,
            StockMovementType Type,
            decimal Quantity,
            string Reason,
            string IdempotencyKey,
            Guid ActorUserId);
    }
}
