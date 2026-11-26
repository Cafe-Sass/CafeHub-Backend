using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class ReceiveStockCommandTests
    {
        private static readonly Guid FixtureTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid OtherBranchId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly DateOnly ReceivedDate = new(2026, 11, 25);
        private static readonly InventoryBatchResponse Response = new(
            Guid.NewGuid(), ItemId, BranchId, 2.5m, 2.5m, ReceivedDate, new DateOnly(2026, 12, 1), Guid.NewGuid());

        [Fact]
        public void Validator_AcceptsACompleteReceiptAndRefusesFieldViolations()
        {
            var validator = new ReceiveStockCommandValidator();
            var valid = Command();

            Assert.True(validator.Validate(valid).IsValid);
            Assert.True(validator.Validate(Command(withExpiry: false)).IsValid);
            Assert.False(validator.Validate(valid with { BranchId = Guid.Empty }).IsValid);
            Assert.False(validator.Validate(valid with { ItemId = Guid.Empty }).IsValid);
            Assert.False(validator.Validate(valid with { ReceivedQuantity = 0m }).IsValid);
            Assert.False(validator.Validate(valid with { ReceivedQuantity = -1m }).IsValid);
            Assert.False(validator.Validate(valid with { ReceivedQuantity = 0.0001m }).IsValid);
            Assert.False(validator.Validate(valid with { ReceivedQuantity = 1_000_000_000m }).IsValid);
            Assert.False(validator.Validate(valid with { ReceivedDate = default }).IsValid);
            Assert.False(validator.Validate(valid with { ExpiryDate = default(DateOnly) }).IsValid);
            Assert.False(validator.Validate(valid with { IdempotencyKey = null }).IsValid);
            Assert.False(validator.Validate(valid with { IdempotencyKey = "short" }).IsValid);
            Assert.False(validator.Validate(valid with { IdempotencyKey = "receipt key 1" }).IsValid);
        }

        [Fact]
        public async Task Handler_ForwardsTheReceiptAndTheAuthenticatedActor()
        {
            var store = new FakeStore();
            var evaluator = new FakeAlertEvaluator();
            var handler = new ReceiveStockCommandHandler(store, evaluator, User(), Context());

            var result = await handler.Handle(Command(), default);

            Assert.Equal(Response, result.Value);
            Assert.Equal((BranchId, ItemId), Assert.Single(evaluator.StockRequests));
            var call = Assert.IsType<ReceiveCall>(store.Received);
            Assert.Equal(BranchId, call.BranchId);
            Assert.Equal(ItemId, call.ItemId);
            Assert.Equal(2.5m, call.ReceivedQuantity);
            Assert.Equal(ReceivedDate, call.ReceivedDate);
            Assert.Equal(new DateOnly(2026, 12, 1), call.ExpiryDate);
            Assert.Equal("receipt-key-1", call.IdempotencyKey);
            Assert.Equal(User().UserId!.Value, call.ActorUserId);
        }

        [Fact]
        public async Task Handler_ForAStaffMemberOfAnotherBranchReturnsTheUniformNotFound()
        {
            var store = new FakeStore();
            var handler = new ReceiveStockCommandHandler(store, new FakeAlertEvaluator(), StaffUser(BranchId), Context());

            var result = await handler.Handle(Command(branchId: OtherBranchId), default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), result.Error);
            Assert.Null(store.Received);
        }

        [Fact]
        public async Task Handler_AllowsTheStaffMembersOwnBranch()
        {
            var store = new FakeStore();
            var handler = new ReceiveStockCommandHandler(store, new FakeAlertEvaluator(), StaffUser(BranchId), Context());

            var result = await handler.Handle(Command(), default);

            Assert.True(result.IsSuccess);
            Assert.NotNull(store.Received);
        }

        [Fact]
        public async Task Handler_RefusesAStaffMemberWithoutAnActiveBranch()
        {
            var store = new FakeStore();
            var user = new FakeTenantUser
            {
                AccountAccess = new TenantAccountAccess(true, "1", false),
            };
            var handler = new ReceiveStockCommandHandler(store, new FakeAlertEvaluator(), user, Context());

            var result = await handler.Handle(Command(), default);

            Assert.Equal(AuthErrors.Forbidden(), result.Error);
            Assert.Null(store.Received);
        }

        [Fact]
        public async Task Handler_RefusesAMissingTenantActorWithTheUniformAuthError()
        {
            var store = new FakeStore();
            var anonymous = new FakeTenantUser { UserId = Guid.Empty };
            var handler = new ReceiveStockCommandHandler(store, new FakeAlertEvaluator(), anonymous, Context());

            var result = await handler.Handle(Command(), default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailable(), result.Error);
            Assert.Null(store.Received);
        }

        [Fact]
        public void Permission_CatalogMakesInventoryReceiveAStaffGrant()
        {
            Assert.Contains(TenantPermissions.InventoryReceive, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.InventoryReceive));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.InventoryReceive));
            Assert.Equal("inventory.receive", TenantPermissions.InventoryReceive);
        }

        private static ReceiveStockCommand Command(Guid? branchId = null, bool withExpiry = true) => new()
        {
            BranchId = branchId ?? BranchId,
            ItemId = ItemId,
            ReceivedQuantity = 2.5m,
            ReceivedDate = ReceivedDate,
            ExpiryDate = withExpiry ? new DateOnly(2026, 12, 1) : null,
            IdempotencyKey = "receipt-key-1",
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

        private sealed class FakeStore : IInventoryBatchStore
        {
            public ReceiveCall? Received { get; private set; }

            public Task<Result<InventoryBatchResponse>> ReceiveAsync(
                Guid branchId,
                Guid itemId,
                decimal receivedQuantity,
                DateOnly receivedDate,
                DateOnly? expiryDate,
                string idempotencyKey,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Received = new ReceiveCall(
                    branchId, itemId, receivedQuantity, receivedDate, expiryDate, idempotencyKey, actorUserId);
                return Task.FromResult(Result.Success(Response));
            }

            public Task<Result<InventoryBatchListPage>> ListAsync(
                InventoryBatchListRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The command tests do not list batches.");

            public Task<InventoryStockLevelPage> ListStockLevelsAsync(
                InventoryStockLevelRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The command tests do not read stock levels.");
        }

        private sealed class FakeAlertEvaluator : IInventoryAlertEvaluator
        {
            public List<(Guid BranchId, Guid ItemId)> StockRequests { get; } = new();

            public Task<Result> EvaluateStockAsync(
                Guid branchId, Guid itemId, CancellationToken cancellationToken)
            {
                StockRequests.Add((branchId, itemId));
                return Task.FromResult(Result.Success());
            }

            public Task<Result> EvaluateExpiryAsync(
                Guid? branchId, Guid? itemId, CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success());
        }

        private sealed record ReceiveCall(
            Guid BranchId,
            Guid ItemId,
            decimal ReceivedQuantity,
            DateOnly ReceivedDate,
            DateOnly? ExpiryDate,
            string IdempotencyKey,
            Guid ActorUserId);
    }
}
