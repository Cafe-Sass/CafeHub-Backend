using CafeHub.Application.Inventory;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Inventory;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryItemStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();
        private const uint Version = 13;
        private const string ActiveNameIndex = "uq_inventory_items_active_name";

        [Fact]
        public async Task Create_StagesTheItemAndAnAuditSnapshotWithEveryChangedField()
        {
            var audit = new RecordingTenantAuditWriter();
            await using var context = new TenantCommandStub(
                (_, _) => throw new InvalidOperationException()).CreateContext(new SaveStub());
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync("  Coffee Beans  ", InventoryUnit.Kg, 2.5m, true, 7, Actor, default);

            Assert.True(result.IsSuccess);
            Assert.Equal("Coffee Beans", result.Value.Name);
            Assert.Equal("Active", result.Value.Status);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("inventory_items.created.succeeded", entry.Action);
            Assert.Equal("inventory_items", entry.TargetType);
            Assert.Equal(Actor, entry.ActorUserId);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Null(snapshot.BeforeState);
            Assert.Contains("\"LowStockThreshold\":\"2.5\"", snapshot.AfterState);
            Assert.Contains("\"TracksExpiry\":\"True\"", snapshot.AfterState);
            Assert.Contains("\"Unit\":\"Kg\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Create_AnActiveNameAlreadyInUse_ReturnsTheNameConflict()
        {
            var audit = new RecordingTenantAuditWriter();
            var save = new SaveStub(_ => throw UniqueViolation());
            await using var context = new TenantCommandStub(
                (_, _) => throw new InvalidOperationException()).CreateContext(save);
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.CreateAsync("COFFEE", InventoryUnit.Kg, 1m, false, 7, Actor, default);

            Assert.Equal(InventoryItemErrors.NameConflict(), result.Error);
            Assert.Single(audit.Entries);
        }

        [Fact]
        public async Task Update_StagesOnlyTheChangedFieldsInTheAuditSnapshot()
        {
            var item = Create("Coffee Beans");
            var audit = new RecordingTenantAuditWriter();
            await using var context = ForItem(item).CreateContext(new SaveStub());
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now.AddMinutes(1)));

            var result = await store.UpdateAsync(
                item.Id, "Coffee Beans Dark", InventoryUnit.Kg, 5m, true, 7, Version, Actor, default);

            Assert.Equal("Coffee Beans Dark", result.Value.Name);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("inventory_items.updated.succeeded", entry.Action);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Equal("{\"Name\":\"Coffee Beans\"}", snapshot.BeforeState);
            Assert.Equal("{\"Name\":\"Coffee Beans Dark\"}", snapshot.AfterState);
        }

        [Fact]
        public async Task Update_WithNoChangedValueReturnsTheCurrentStateWithoutAuditing()
        {
            var item = Create("Coffee Beans");
            var audit = new RecordingTenantAuditWriter();
            await using var context = ForItem(item).CreateContext(new SaveStub());
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now.AddMinutes(1)));

            var result = await store.UpdateAsync(
                item.Id, " Coffee Beans ", InventoryUnit.Kg, 5m, true, 7, Version, Actor, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(Now, item.UpdatedAt);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Update_WithAStaleTokenRefusesBeforeAnyAuditOrSave()
        {
            var item = Create("Coffee Beans");
            var audit = new RecordingTenantAuditWriter();
            await using var context = ForItem(item).CreateContext(new SaveStub());
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.UpdateAsync(
                item.Id, "Other", InventoryUnit.Kg, 5m, true, 7, Version - 1, Actor, default);

            Assert.Equal(InventoryItemErrors.ConcurrencyConflict(), result.Error);
            Assert.Empty(audit.Entries);
            Assert.Equal("Coffee Beans", item.Name);
        }

        [Fact]
        public async Task Update_AConcurrentWriterWinsFirst_ReturnsTheConcurrencyConflict()
        {
            var item = Create("Coffee Beans");
            var audit = new RecordingTenantAuditWriter();
            await using var context = ForItem(item)
                .CreateContext(new SaveStub(_ => throw new DbUpdateConcurrencyException("Fixture stale write.")));
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.UpdateAsync(
                item.Id, "Other", InventoryUnit.Kg, 5m, true, 7, Version, Actor, default);

            Assert.Equal(InventoryItemErrors.ConcurrencyConflict(), result.Error);
            Assert.Single(audit.Entries);
        }

        [Fact]
        public async Task Deactivate_KeepsTheRowAndAuditsTheStatusTransition()
        {
            var item = Create("Coffee Beans");
            var audit = new RecordingTenantAuditWriter();
            await using var context = ForItem(item).CreateContext(new SaveStub());
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now.AddMinutes(1)));

            var result = await store.ChangeStatusAsync(
                item.Id, InventoryItemStatus.Inactive, Version, Actor, default);

            Assert.Equal("Inactive", result.Value.Status);
            Assert.Equal("Coffee Beans", result.Value.Name);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("inventory_items.deactivated.succeeded", entry.Action);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Equal("{\"Status\":\"Active\"}", snapshot.BeforeState);
            Assert.Equal("{\"Status\":\"Inactive\"}", snapshot.AfterState);
        }

        [Fact]
        public async Task Reactivate_WhenAnActiveRowHoldsTheName_ReturnsTheNameConflict()
        {
            var item = Create("Coffee Beans");
            item.Deactivate(Now, Actor);
            var audit = new RecordingTenantAuditWriter();
            await using var context = ForItem(item).CreateContext(new SaveStub(_ => throw UniqueViolation()));
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now.AddMinutes(1)));

            var result = await store.ChangeStatusAsync(
                item.Id, InventoryItemStatus.Active, Version, Actor, default);

            Assert.Equal(InventoryItemErrors.NameConflict(), result.Error);
            Assert.Equal("inventory_items.reactivated.succeeded", Assert.Single(audit.Entries).Action);
        }

        [Fact]
        public async Task ChangeStatus_OnAMissingItemReturnsNotFound()
        {
            var audit = new RecordingTenantAuditWriter();
            await using var context = new TenantCommandStub(
                (_, _) => new System.Data.DataTable()).CreateContext(new SaveStub());
            var store = new InventoryItemStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ChangeStatusAsync(
                Guid.NewGuid(), InventoryItemStatus.Active, Version, Actor, default);

            Assert.Equal(InventoryItemErrors.NotFound(), result.Error);
            Assert.Empty(audit.Entries);
        }

        private static TenantCommandStub ForItem(InventoryItem item) =>
            new((command, data) => TenantCommandStub.EntityRow(command, data, item, Version));

        private static InventoryItem Create(string name) =>
            InventoryItem.Create(Guid.CreateVersion7(), name, InventoryUnit.Kg, 5m, true, 7, Now, Actor);

        private static DbUpdateException UniqueViolation() =>
            new("Fixture duplicate name.", new PostgresException(
                "Fixture collision.", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                constraintName: ActiveNameIndex));

        private sealed class SaveStub(Action<DbContext>? check = null) : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                check?.Invoke(eventData.Context!);
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }
    }
}
