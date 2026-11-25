using System.Data;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Inventory;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Inventory;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class StockMovementStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly Guid BatchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly DateOnly ReceivedDate = new(2026, 11, 25);

        [Fact]
        public async Task Record_AdjustmentMovesTheBatchAndWritesOneAuditSnapshot()
        {
            var batch = CreateBatch(5m);
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(CreateItem(), CreateBranch(), batch)
                .CreateContext(new SaveStub());
            var store = new StockMovementStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.RecordAsync(
                BranchId, ItemId, batch.Id, StockMovementType.Adjust, -2m,
                "count correction", "movement-key-1", Actor, default);

            Assert.True(result.IsSuccess);
            var response = Assert.Single(result.Value);
            Assert.Equal("Adjust", response.Type);
            Assert.Equal(-2m, response.Quantity);
            Assert.Equal("count correction", response.Reason);
            Assert.Equal(3m, context.InventoryBatches.Local.Single().RemainingQuantity);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("stock_movements.adjust.succeeded", entry.Action);
            Assert.Equal("stock_movements", entry.TargetType);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Contains("\"Quantity\":\"-2\"", snapshot.AfterState);
            Assert.Contains("\"Reason\":\"count correction\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Record_RefusesAConsumeAboveTheAvailableStockWithoutMovingBatches()
        {
            var batch = CreateBatch(2.5m);
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(CreateItem(), CreateBranch(), batch)
                .CreateContext(new SaveStub());
            var store = new StockMovementStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.RecordAsync(
                BranchId, ItemId, null, StockMovementType.Consume, 2.501m,
                "spillage", "movement-key-1", Actor, default);

            Assert.Equal(StockMovementErrors.InsufficientStock(), result.Error);
            Assert.Equal(2.5m, context.InventoryBatches.Local.Single().RemainingQuantity);
            Assert.Empty(context.StockMovements.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Record_AdjustmentAboveTheReceivedQuantityAndMissingBatchAreRefused()
        {
            var batch = CreateBatch(5m);
            var audit = new RecordingTenantAuditWriter();
            await using var aboveContext = Stub(CreateItem(), CreateBranch(), batch)
                .CreateContext(new SaveStub());
            var above = await new StockMovementStore(aboveContext, audit, new FixedTimeProvider(Now))
                .RecordAsync(
                    BranchId, ItemId, batch.Id, StockMovementType.Adjust, 1m,
                    "count correction", "movement-key-1", Actor, default);

            await using var missingContext = Stub(CreateItem(), CreateBranch())
                .CreateContext(new SaveStub());
            var missing = await new StockMovementStore(missingContext, audit, new FixedTimeProvider(Now))
                .RecordAsync(
                    BranchId, ItemId, BatchId, StockMovementType.Adjust, -1m,
                    "count correction", "movement-key-1", Actor, default);

            Assert.Equal(StockMovementErrors.AdjustmentExceedsReceived(), above.Error);
            Assert.Equal(StockMovementErrors.BatchNotFound(), missing.Error);
            Assert.Equal(5m, aboveContext.InventoryBatches.Local.Single().RemainingQuantity);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Record_RepeatedWithTheSameKeyReturnsTheStoredMovementsWithoutWritingAgain()
        {
            var stored = CreateMovement(StockMovementType.Consume, -2.5m, "spillage");
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(stored: stored).CreateContext(new SaveStub());
            var store = new StockMovementStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.RecordAsync(
                BranchId, ItemId, null, StockMovementType.Consume, 2.5m,
                "spillage", "movement-key-1", Actor, default);

            var response = Assert.Single(result.Value);
            Assert.Equal(stored.Id, response.Id);
            Assert.Empty(context.StockMovements.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Record_RepeatedWithADifferentPayloadUnderTheSameKeyIsRefused()
        {
            var stored = CreateMovement(StockMovementType.Consume, -2.5m, "spillage");
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(stored: stored).CreateContext(new SaveStub());
            var store = new StockMovementStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.RecordAsync(
                BranchId, ItemId, null, StockMovementType.Consume, 3m,
                "spillage", "movement-key-1", Actor, default);

            Assert.Equal(IdempotencyErrors.KeyReused(), result.Error);
            Assert.Empty(context.StockMovements.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Record_RefusesAnInactiveBranchInactiveItemAndMissingRecords()
        {
            var audit = new RecordingTenantAuditWriter();

            await using var suspendedContext = Stub(
                    CreateItem(), CreateBranch(BranchLifecycleStatus.Suspended), CreateBatch(5m))
                .CreateContext(new SaveStub());
            var suspended = await new StockMovementStore(
                    suspendedContext, audit, new FixedTimeProvider(Now))
                .RecordAsync(
                    BranchId, ItemId, null, StockMovementType.Consume, 1m,
                    "spillage", "movement-key-1", Actor, default);

            var inactiveItem = CreateItem();
            inactiveItem.Deactivate(Now, Actor);
            await using var inactiveContext = Stub(inactiveItem, CreateBranch(), CreateBatch(5m))
                .CreateContext(new SaveStub());
            var inactive = await new StockMovementStore(inactiveContext, audit, new FixedTimeProvider(Now))
                .RecordAsync(
                    BranchId, ItemId, null, StockMovementType.Consume, 1m,
                    "spillage", "movement-key-1", Actor, default);

            await using var missingContext = Stub(null, CreateBranch(), CreateBatch(5m))
                .CreateContext(new SaveStub());
            var missing = await new StockMovementStore(missingContext, audit, new FixedTimeProvider(Now))
                .RecordAsync(
                    BranchId, ItemId, null, StockMovementType.Consume, 1m,
                    "spillage", "movement-key-1", Actor, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), suspended.Error);
            Assert.Equal(InventoryBatchErrors.ItemInactive(), inactive.Error);
            Assert.Equal(InventoryItemErrors.NotFound(), missing.Error);
            Assert.Empty(audit.Entries);
        }

        private static TenantCommandStub Stub(
            InventoryItem? item = null,
            Branch? branch = null,
            InventoryBatch? batch = null,
            StockMovement? stored = null) =>
            new((command, eventData) =>
            {
                if (command.CommandText.Contains("FROM stock_movements", StringComparison.Ordinal))
                {
                    return stored is null
                        ? Empty(TenantCommandStub.EntityRow(command, eventData, CreateMovement()))
                        : TenantCommandStub.EntityRow(command, eventData, stored);
                }

                if (command.CommandText.Contains("FROM inventory_batches", StringComparison.Ordinal))
                {
                    return batch is null
                        ? Empty(TenantCommandStub.EntityRow(command, eventData, CreateBatch(1m)))
                        : TenantCommandStub.EntityRow(command, eventData, batch);
                }

                if (command.CommandText.Contains("FROM branches", StringComparison.Ordinal))
                {
                    return branch is null
                        ? Empty(TenantCommandStub.EntityRow(command, eventData, CreateBranch()))
                        : TenantCommandStub.EntityRow(command, eventData, branch);
                }

                return item is null
                    ? Empty(TenantCommandStub.EntityRow(command, eventData, CreateItem()))
                    : TenantCommandStub.EntityRow(command, eventData, item);
            });

        private static InventoryItem CreateItem() =>
            InventoryItem.Create(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                "Coffee Beans", InventoryUnit.Kg, 5m, false, 7, Now, Actor);

        private static Branch CreateBranch(BranchLifecycleStatus status = BranchLifecycleStatus.Active) =>
            Branch.Create(BranchId, "Downtown", status, null, null, null, Now, Actor);

        private static InventoryBatch CreateBatch(decimal quantity) =>
            InventoryBatch.Create(
                BatchId, ItemId, BranchId, quantity, ReceivedDate, null, false,
                Actor, "receipt-key-1", Now, Actor);

        private static StockMovement CreateMovement(
            StockMovementType type = StockMovementType.Consume,
            decimal quantity = -1m,
            string reason = "spillage") =>
            StockMovement.Create(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                ItemId, BranchId, BatchId, type, quantity, reason,
                "movement-key-1", Now, Actor);

        private static DataTable Empty(DataTable table)
        {
            table.Rows.Clear();
            return table;
        }

        private sealed class SaveStub : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result,
                CancellationToken cancellationToken = default) =>
                ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
        }
    }
}
