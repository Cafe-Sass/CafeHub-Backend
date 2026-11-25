using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Inventory.Commands;
using CafeHub.Application.Inventory.Contracts;
using CafeHub.Application.Inventory.DTOs;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.UnitTests.Branches;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryItemCommandTests
    {
        private static readonly Guid TenantId = Guid.NewGuid();
        private static readonly InventoryItemResponse Response =
            new(Guid.NewGuid(), "Coffee Beans", "Kg", 5m, true, 7, "Active", "1");

        [Fact]
        public void CreateValidator_AcceptsAValidItemAndRefusesFieldViolations()
        {
            var validator = new CreateInventoryItemCommandValidator();

            Assert.True(validator.Validate(
                new CreateInventoryItemCommand("Coffee Beans", InventoryUnit.Kg, 5m, true, null)).IsValid);
            Assert.True(validator.Validate(
                new CreateInventoryItemCommand("Coffee Beans", InventoryUnit.Kg, 5m, false, 0)).IsValid);
            Assert.False(validator.Validate(
                new CreateInventoryItemCommand("  ", InventoryUnit.Kg, 5m, true, 7)).IsValid);
            Assert.False(validator.Validate(
                new CreateInventoryItemCommand("Coffee", (InventoryUnit)99, 5m, true, 7)).IsValid);
            Assert.False(validator.Validate(
                new CreateInventoryItemCommand("Coffee", InventoryUnit.Kg, -1m, true, 7)).IsValid);
            Assert.False(validator.Validate(
                new CreateInventoryItemCommand("Coffee", InventoryUnit.Kg, 0.0001m, true, 7)).IsValid);
            Assert.False(validator.Validate(
                new CreateInventoryItemCommand("Coffee", InventoryUnit.Kg, 5m, true, -1)).IsValid);
        }

        [Fact]
        public void UpdateAndLifecycleValidators_RequireAnIdAndAConcurrencyToken()
        {
            var update = new UpdateInventoryItemCommandValidator();
            var valid = new UpdateInventoryItemCommand(
                Guid.NewGuid(), "Coffee", InventoryUnit.Pack, 2m, false, 0, "13");

            Assert.True(update.Validate(valid).IsValid);
            Assert.False(update.Validate(valid with { Id = Guid.Empty }).IsValid);
            Assert.False(update.Validate(valid with { ConcurrencyToken = " " }).IsValid);
            Assert.False(update.Validate(valid with { ConcurrencyToken = "4294967296" }).IsValid);
            Assert.False(update.Validate(valid with { Name = null }).IsValid);

            Assert.True(new DeactivateInventoryItemCommandValidator()
                .Validate(new DeactivateInventoryItemCommand(Guid.NewGuid(), "13")).IsValid);
            Assert.False(new DeactivateInventoryItemCommandValidator()
                .Validate(new DeactivateInventoryItemCommand(Guid.NewGuid(), "stale")).IsValid);
            Assert.True(new ReactivateInventoryItemCommandValidator()
                .Validate(new ReactivateInventoryItemCommand(Guid.NewGuid(), "13")).IsValid);
            Assert.False(new ReactivateInventoryItemCommandValidator()
                .Validate(new ReactivateInventoryItemCommand(Guid.Empty, "13")).IsValid);
        }

        [Fact]
        public async Task CreateHandler_UsesTheDefaultNearExpiryWindowAndTheAuthenticatedActor()
        {
            var store = new FakeStore();
            var handler = new CreateInventoryItemCommandHandler(store, User(), Context());

            var result = await handler.Handle(
                new CreateInventoryItemCommand(" Coffee Beans ", InventoryUnit.Pack, 2.5m, true, null), default);

            Assert.Equal(Response, result.Value);
            var created = Assert.IsType<FakeStore.CreateCall>(store.Created);
            Assert.Equal(" Coffee Beans ", created.Name);
            Assert.Equal(InventoryUnit.Pack, created.Unit);
            Assert.Equal(2.5m, created.LowStockThreshold);
            Assert.Equal(InventoryItem.DefaultNearExpiryDays, created.NearExpiryDays);
            Assert.Equal(User().UserId!.Value, created.ActorUserId);
        }

        [Fact]
        public async Task Handlers_RefuseAMissingTenantActorWithTheUniformAuthError()
        {
            var store = new FakeStore();
            var anonymous = new FakeCurrentTenantUser
            {
                IsAuthenticated = true,
                UserId = Guid.Empty,
                TenantId = TenantId,
            };
            var handler = new UpdateInventoryItemCommandHandler(store, anonymous, Context());

            var result = await handler.Handle(
                new UpdateInventoryItemCommand(Guid.NewGuid(), "Coffee", InventoryUnit.Kg, 1m, true, 7, "1"),
                default);

            Assert.Equal(AuthErrors.CurrentTenantUserUnavailable(), result.Error);
            Assert.Null(store.Updated);
        }

        [Fact]
        public async Task UpdateHandler_RefusesAnUnparsableTokenBeforeTheStore()
        {
            var store = new FakeStore();
            var handler = new UpdateInventoryItemCommandHandler(store, User(), Context());

            var result = await handler.Handle(
                new UpdateInventoryItemCommand(Guid.NewGuid(), "Coffee", InventoryUnit.Kg, 1m, true, 7, "stale"),
                default);

            var error = Assert.IsType<ValidationError>(result.Error);
            Assert.Equal("concurrencyToken", Assert.Single(error.Errors).Field);
            Assert.Null(store.Updated);
        }

        [Fact]
        public void Permission_CatalogMakesInventoryManageAStaffGrant()
        {
            Assert.Contains(TenantPermissions.InventoryManage, TenantPermissions.All);
            Assert.True(TenantPermissions.IsStaffGrant(TenantPermissions.InventoryManage));
            Assert.False(TenantPermissions.IsOwnerOnly(TenantPermissions.InventoryManage));
            Assert.Equal("inventory.manage", TenantPermissions.InventoryManage);
        }

        private static FakeCurrentTenantUser User() => new()
        {
            IsAuthenticated = true,
            UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            TenantId = TenantId,
            Roles = new[] { "Owner" },
        };

        private static StubTenantContextAccessor Context() =>
            new(TenantContext.Create(TenantId, "fixture", true));

        private sealed class FakeStore : IInventoryItemStore
        {
            public CreateCall? Created { get; private set; }

            public UpdateCall? Updated { get; private set; }

            public Task<Result<InventoryItemResponse>> CreateAsync(
                string name,
                InventoryUnit unit,
                decimal lowStockThreshold,
                bool tracksExpiry,
                int nearExpiryDays,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Created = new CreateCall(name, unit, lowStockThreshold, tracksExpiry, nearExpiryDays, actorUserId);
                return Task.FromResult(Result.Success(Response));
            }

            public Task<Result<InventoryItemResponse>> UpdateAsync(
                Guid id,
                string name,
                InventoryUnit unit,
                decimal lowStockThreshold,
                bool tracksExpiry,
                int nearExpiryDays,
                uint version,
                Guid actorUserId,
                CancellationToken cancellationToken)
            {
                Updated = new UpdateCall(
                    id, name, unit, lowStockThreshold, tracksExpiry, nearExpiryDays, version, actorUserId);
                return Task.FromResult(Result.Success(Response));
            }

            public Task<Result<InventoryItemResponse>> ChangeStatusAsync(
                Guid id,
                InventoryItemStatus status,
                uint version,
                Guid actorUserId,
                CancellationToken cancellationToken) =>
                Task.FromResult(Result.Success(Response));

            public Task<InventoryItemResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The command tests do not read items.");

            public Task<InventoryItemListPage> ListAsync(
                InventoryItemListRequest request, CancellationToken cancellationToken) =>
                throw new NotSupportedException("The command tests do not list items.");

            internal sealed record CreateCall(
                string Name,
                InventoryUnit Unit,
                decimal LowStockThreshold,
                bool TracksExpiry,
                int NearExpiryDays,
                Guid ActorUserId);

            internal sealed record UpdateCall(
                Guid Id,
                string Name,
                InventoryUnit Unit,
                decimal LowStockThreshold,
                bool TracksExpiry,
                int NearExpiryDays,
                uint Version,
                Guid ActorUserId);
        }
    }
}
