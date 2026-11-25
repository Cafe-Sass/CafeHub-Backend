using System.Data;
using CafeHub.Application.Idempotency;
using CafeHub.Application.Inventory;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Inventory;
using CafeHub.UnitTests.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryBatchStoreTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly DateOnly ReceivedDate = new(2026, 11, 25);
        private static readonly DateOnly ExpiryDate = new(2026, 12, 1);
        private const string IdempotencyIndex = "uq_inventory_batches_branch_idempotency_key";

        [Fact]
        public async Task Receive_OnAnActiveBranch_CreatesTheBatchAndAuditsTheReceipt()
        {
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(CreateItem(), CreateBranch())
                .CreateContext(new SaveStub());
            var store = new InventoryBatchStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ReceiveAsync(
                BranchId, ItemId, 2.5m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            Assert.True(result.IsSuccess);
            Assert.Equal(2.5m, result.Value.ReceivedQuantity);
            Assert.Equal(2.5m, result.Value.RemainingQuantity);
            Assert.Equal(ExpiryDate, result.Value.ExpiryDate);
            var batch = Assert.Single(context.InventoryBatches.Local);
            Assert.Equal(2.5m, batch.RemainingQuantity);
            var entry = Assert.Single(audit.Entries);
            Assert.Equal("inventory_batches.received.succeeded", entry.Action);
            Assert.Equal("inventory_batches", entry.TargetType);
            Assert.Equal(batch.Id, entry.TargetId);
            Assert.Equal(Actor, entry.ActorUserId);
            var snapshot = TenantAuditSnapshotSerializer.Create(entry.TargetType, entry.Changes!).Value;
            Assert.Null(snapshot.BeforeState);
            Assert.Contains($"\"BranchId\":\"{BranchId}\"", snapshot.AfterState);
            Assert.Contains("\"ReceivedQuantity\":\"2.5\"", snapshot.AfterState);
            Assert.Contains("\"ExpiryDate\":\"2026-12-01\"", snapshot.AfterState);
        }

        [Fact]
        public async Task Receive_ForAnItemWithoutExpiryStoresNoExpiryDate()
        {
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(CreateItem(tracksExpiry: false), CreateBranch())
                .CreateContext(new SaveStub());
            var store = new InventoryBatchStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ReceiveAsync(
                BranchId, ItemId, 1m, ReceivedDate, null, "receipt-key-1", Actor, default);

            Assert.True(result.IsSuccess);
            Assert.Null(result.Value.ExpiryDate);
            Assert.DoesNotContain("\"ExpiryDate\"", Assert.Single(audit.Entries).Changes!.Fields
                .Select(field => field.Name));
        }

        [Fact]
        public async Task Receive_RepeatedWithTheSameKeyReturnsTheStoredBatchWithoutInsertingAgain()
        {
            var stored = CreateBatch();
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(CreateItem(), CreateBranch(), stored: stored)
                .CreateContext(new SaveStub());
            var store = new InventoryBatchStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ReceiveAsync(
                BranchId, ItemId, 2.5m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            Assert.Equal(stored.Id, result.Value.Id);
            Assert.Empty(context.InventoryBatches.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Receive_RepeatedWithDifferentValuesUnderTheSameKeyIsRefused()
        {
            var stored = CreateBatch();
            var audit = new RecordingTenantAuditWriter();
            await using var context = Stub(CreateItem(), CreateBranch(), stored: stored)
                .CreateContext(new SaveStub());
            var store = new InventoryBatchStore(context, audit, new FixedTimeProvider(Now));

            var otherQuantity = await store.ReceiveAsync(
                BranchId, ItemId, 5m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);
            var otherItem = await store.ReceiveAsync(
                BranchId, Guid.NewGuid(), 2.5m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            Assert.Equal(IdempotencyErrors.KeyReused(), otherQuantity.Error);
            Assert.Equal(IdempotencyErrors.KeyReused(), otherItem.Error);
            Assert.Empty(context.InventoryBatches.Local);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Receive_AConcurrentRetryWinsTheKey_ReturnsTheStoredBatch()
        {
            var stored = CreateBatch();
            var reads = 0;
            var audit = new RecordingTenantAuditWriter();
            var stub = new TenantCommandStub((command, eventData) =>
            {
                if (command.CommandText.Contains("FROM inventory_batches", StringComparison.Ordinal))
                {
                    reads++;
                    return reads == 1
                        ? Empty(TenantCommandStub.EntityRow(command, eventData, stored))
                        : TenantCommandStub.EntityRow(command, eventData, stored);
                }

                if (command.CommandText.Contains("FROM branches", StringComparison.Ordinal))
                {
                    return TenantCommandStub.EntityRow(command, eventData, CreateBranch());
                }

                return TenantCommandStub.EntityRow(command, eventData, CreateItem());
            });
            await using var context = stub.CreateContext(new SaveStub(_ => throw UniqueViolation()));
            var store = new InventoryBatchStore(context, audit, new FixedTimeProvider(Now));

            var result = await store.ReceiveAsync(
                BranchId, ItemId, 2.5m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            Assert.Equal(stored.Id, result.Value.Id);
            Assert.Equal(2, reads);
            Assert.Empty(context.InventoryBatches.Local);
        }

        [Fact]
        public async Task Receive_RefusesAnInactiveBranchInactiveItemAndMissingRecords()
        {
            var audit = new RecordingTenantAuditWriter();

            await using var suspendedBranchContext = Stub(
                    CreateItem(), CreateBranch(BranchLifecycleStatus.Suspended))
                .CreateContext(new SaveStub());
            var suspended = await new InventoryBatchStore(
                    suspendedBranchContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(BranchId, ItemId, 1m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            await using var missingBranchContext = Stub(CreateItem(), branch: null)
                .CreateContext(new SaveStub());
            var missingBranch = await new InventoryBatchStore(
                    missingBranchContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(BranchId, ItemId, 1m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            var inactiveItem = CreateItem();
            inactiveItem.Deactivate(Now, Actor);
            await using var inactiveItemContext = Stub(inactiveItem, CreateBranch())
                .CreateContext(new SaveStub());
            var inactive = await new InventoryBatchStore(
                    inactiveItemContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(BranchId, ItemId, 1m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            await using var missingItemContext = Stub(null, CreateBranch())
                .CreateContext(new SaveStub());
            var missingItem = await new InventoryBatchStore(
                    missingItemContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(BranchId, ItemId, 1m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            Assert.Equal(InventoryBatchErrors.BranchNotFound(), suspended.Error);
            Assert.Equal(InventoryBatchErrors.BranchNotFound(), missingBranch.Error);
            Assert.Equal(InventoryBatchErrors.ItemInactive(), inactive.Error);
            Assert.Equal(InventoryItemErrors.NotFound(), missingItem.Error);
            Assert.Empty(audit.Entries);
        }

        [Fact]
        public async Task Receive_RefusesAnExpiryMismatchBeforeWriting()
        {
            var audit = new RecordingTenantAuditWriter();

            await using var missingExpiryContext = Stub(CreateItem(), CreateBranch())
                .CreateContext(new SaveStub());
            var missingExpiry = await new InventoryBatchStore(
                    missingExpiryContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(BranchId, ItemId, 1m, ReceivedDate, null, "receipt-key-1", Actor, default);

            await using var notTrackedContext = Stub(CreateItem(tracksExpiry: false), CreateBranch())
                .CreateContext(new SaveStub());
            var notTracked = await new InventoryBatchStore(
                    notTrackedContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(BranchId, ItemId, 1m, ReceivedDate, ExpiryDate, "receipt-key-1", Actor, default);

            await using var earlierExpiryContext = Stub(CreateItem(), CreateBranch())
                .CreateContext(new SaveStub());
            var earlierExpiry = await new InventoryBatchStore(
                    earlierExpiryContext, audit, new FixedTimeProvider(Now))
                .ReceiveAsync(
                    BranchId, ItemId, 1m, ReceivedDate, ReceivedDate.AddDays(-1), "receipt-key-1", Actor, default);

            Assert.Equal(InventoryBatchErrors.ExpiryRequired(), missingExpiry.Error);
            Assert.Equal(InventoryBatchErrors.ExpiryNotTracked(), notTracked.Error);
            Assert.Equal(InventoryBatchErrors.ExpiryBeforeReceived(), earlierExpiry.Error);
            Assert.Empty(audit.Entries);
        }

        private static TenantCommandStub Stub(InventoryItem? item, Branch? branch, InventoryBatch? stored = null) =>
            new((command, eventData) =>
            {
                if (command.CommandText.Contains("FROM inventory_batches", StringComparison.Ordinal))
                {
                    return stored is null
                        ? Empty(TenantCommandStub.EntityRow(command, eventData, CreateBatch()))
                        : TenantCommandStub.EntityRow(command, eventData, stored);
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

        private static InventoryItem CreateItem(bool tracksExpiry = true) =>
            InventoryItem.Create(Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                "Coffee Beans", InventoryUnit.Kg, 5m, tracksExpiry, 7, Now, Actor);

        private static Branch CreateBranch(BranchLifecycleStatus status = BranchLifecycleStatus.Active) =>
            Branch.Create(BranchId, "Downtown", status, null, null, null, Now, Actor);

        private static InventoryBatch CreateBatch() =>
            InventoryBatch.Create(Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ItemId, BranchId, 2.5m, ReceivedDate, ExpiryDate, true, Actor,
                "receipt-key-1", Now, Actor);

        private static DataTable Empty(DataTable table)
        {
            table.Rows.Clear();
            return table;
        }

        private static DbUpdateException UniqueViolation() =>
            new("Fixture duplicate receipt.", new PostgresException(
                "Fixture collision.", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation,
                constraintName: IdempotencyIndex));

        private sealed class SaveStub(Action<DbContext>? check = null) : SaveChangesInterceptor
        {
            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                check?.Invoke(eventData.Context!);
                return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(1));
            }
        }
    }
}
