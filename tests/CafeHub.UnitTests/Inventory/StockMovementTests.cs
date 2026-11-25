using CafeHub.Application.Inventory;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class StockMovementTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        private static readonly DateOnly Today = new(2026, 11, 25);
        private static readonly Guid Actor = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

        [Fact]
        public void Allocation_UsesEarliestExpiryThenOldestReceiptAcrossBatches()
        {
            var earliestExpiry = Batch("11111111-1111-1111-1111-111111111111", 5m, new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 20));
            var sameExpiryOlderReceipt = Batch("22222222-2222-2222-2222-222222222222", 5m, new DateOnly(2026, 12, 1), new DateOnly(2026, 11, 18));
            var laterExpiry = Batch("33333333-3333-3333-3333-333333333333", 5m, new DateOnly(2026, 12, 10), new DateOnly(2026, 11, 1));

            var allocated = StockAllocation.TryAllocate(
                new[] { earliestExpiry, sameExpiryOlderReceipt, laterExpiry },
                8m,
                Today,
                allowExpired: false,
                out var allocations);

            Assert.True(allocated);
            Assert.Equal(2, allocations.Count);
            Assert.Equal(sameExpiryOlderReceipt.Id, allocations[0].BatchId);
            Assert.Equal(5m, allocations[0].Quantity);
            Assert.Equal(earliestExpiry.Id, allocations[1].BatchId);
            Assert.Equal(3m, allocations[1].Quantity);
        }

        [Fact]
        public void Allocation_RefusesAQuantityAboveTheAvailableStock()
        {
            var batch = Batch("11111111-1111-1111-1111-111111111111", 2.5m, null, Today);

            var allocated = StockAllocation.TryAllocate(
                new[] { batch }, 2.501m, Today, allowExpired: true, out var allocations);

            Assert.False(allocated);
            Assert.Empty(allocations);
            Assert.Equal(2.5m, batch.RemainingQuantity);
        }

        [Fact]
        public void Allocation_ForConsumeSkipsExpiredBatchesButWasteIncludesThem()
        {
            var expired = Batch("11111111-1111-1111-1111-111111111111", 5m, Today.AddDays(-1), Today.AddDays(-10));
            var fresh = Batch("22222222-2222-2222-2222-222222222222", 5m, Today.AddDays(1), Today);

            var consumed = StockAllocation.TryAllocate(
                new[] { expired, fresh }, 6m, Today, allowExpired: false, out var consumeAllocations);
            var wasted = StockAllocation.TryAllocate(
                new[] { expired, fresh }, 6m, Today, allowExpired: true, out var wasteAllocations);

            Assert.False(consumed);
            Assert.Empty(consumeAllocations);
            Assert.True(wasted);
            Assert.Equal(2, wasteAllocations.Count);
            Assert.Equal(expired.Id, wasteAllocations[0].BatchId);
            Assert.Equal(5m, wasteAllocations[0].Quantity);
            Assert.Equal(fresh.Id, wasteAllocations[1].BatchId);
            Assert.Equal(1m, wasteAllocations[1].Quantity);
        }

        [Fact]
        public void BatchMovement_CanNeverLeaveTheRemainingQuantityOutsideTheReceivedRange()
        {
            var batch = Batch("11111111-1111-1111-1111-111111111111", 5m, null, Today);

            Assert.False(batch.CanApplyMovement(-5.001m));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.ApplyMovement(-5.001m, Now, Actor));
            Assert.Equal(5m, batch.RemainingQuantity);

            batch.ApplyMovement(-2.5m, Now, Actor);
            Assert.Equal(2.5m, batch.RemainingQuantity);
            Assert.True(batch.CanApplyMovement(2.5m));
            Assert.False(batch.CanApplyMovement(2.501m));
            Assert.Throws<ArgumentOutOfRangeException>(() => batch.ApplyMovement(2.501m, Now, Actor));
            Assert.Equal(2.5m, batch.RemainingQuantity);
        }

        [Fact]
        public void MovementTypesEnforceTheirSignAndReasonRules()
        {
            var receive = Movement(StockMovementType.Receive, 1m, reason: null);
            var consume = Movement(StockMovementType.Consume, -1m, "spillage");
            var waste = Movement(StockMovementType.Waste, -1m, "expired");
            var adjust = Movement(StockMovementType.Adjust, -1m, "count correction");

            Assert.Equal(-1m, consume.Quantity);
            Assert.Equal(-1m, waste.Quantity);
            Assert.Equal(-1m, adjust.Quantity);
            Assert.Null(receive.Reason);

            Assert.Throws<ArgumentOutOfRangeException>(() => Movement(StockMovementType.Receive, -1m, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Movement(StockMovementType.Consume, 1m, "spillage"));
            Assert.Throws<ArgumentOutOfRangeException>(() => Movement(StockMovementType.Adjust, 0m, "count correction"));
            Assert.Throws<ArgumentException>(() => Movement(StockMovementType.Adjust, 1m, null));
            Assert.Throws<ArgumentException>(() => Movement(StockMovementType.Adjust, 1m, " "));
            Assert.Throws<ArgumentException>(() => Movement(StockMovementType.Receive, 1m, "unexpected"));
        }

        [Fact]
        public void Movement_RequiresIdentifiersAnActorAndAValidIdempotencyKey()
        {
            Assert.Throws<ArgumentException>(() => StockMovement.Create(
                Guid.Empty, ItemId, BranchId, Guid.NewGuid(), StockMovementType.Consume, -1m,
                "spillage", "movement-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => StockMovement.Create(
                Guid.NewGuid(), Guid.Empty, BranchId, Guid.NewGuid(), StockMovementType.Consume, -1m,
                "spillage", "movement-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => StockMovement.Create(
                Guid.NewGuid(), ItemId, Guid.Empty, Guid.NewGuid(), StockMovementType.Consume, -1m,
                "spillage", "movement-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => StockMovement.Create(
                Guid.NewGuid(), ItemId, BranchId, Guid.Empty, StockMovementType.Consume, -1m,
                "spillage", "movement-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => StockMovement.Create(
                Guid.NewGuid(), ItemId, BranchId, Guid.NewGuid(), StockMovementType.Consume, -1m,
                "spillage", "movement-key-1", Now, Guid.Empty));
            Assert.Throws<ArgumentException>(() => StockMovement.Create(
                Guid.NewGuid(), ItemId, BranchId, Guid.NewGuid(), StockMovementType.Consume, -1m,
                "spillage", "short", Now, Actor));
        }

        [Fact]
        public void Replay_MatchesTheSameRequestAndRejectsAnyDifferentPayload()
        {
            var batchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var consume = Movement(StockMovementType.Consume, -5m, "spillage", batchId);

            Assert.True(StockMovementReplay.Matches(
                new[] { consume }, ItemId, StockMovementType.Consume, "spillage", 5m, null));
            Assert.False(StockMovementReplay.Matches(
                new[] { consume }, ItemId, StockMovementType.Consume, "spillage", 4m, null));
            Assert.False(StockMovementReplay.Matches(
                new[] { consume }, ItemId, StockMovementType.Waste, "spillage", 5m, null));
            Assert.False(StockMovementReplay.Matches(
                new[] { consume }, ItemId, StockMovementType.Consume, "other", 5m, null));

            var adjust = Movement(StockMovementType.Adjust, -2m, "count correction", batchId);
            Assert.True(StockMovementReplay.Matches(
                new[] { adjust }, ItemId, StockMovementType.Adjust, "count correction", -2m, batchId));
            Assert.False(StockMovementReplay.Matches(
                new[] { adjust }, ItemId, StockMovementType.Adjust, "count correction", -2m, Guid.NewGuid()));
            Assert.False(StockMovementReplay.Matches(
                Array.Empty<StockMovement>(), ItemId, StockMovementType.Adjust, "count correction", -2m, batchId));
        }

        private static InventoryBatch Batch(
            string id, decimal quantity, DateOnly? expiryDate, DateOnly receivedDate)
        {
            var batch = InventoryBatch.Create(
                Guid.Parse(id),
                ItemId,
                BranchId,
                quantity,
                receivedDate,
                expiryDate,
                expiryDate is not null,
                Actor,
                "receipt-key-1",
                Now,
                Actor);
            return batch;
        }

        private static StockMovement Movement(
            StockMovementType type,
            decimal quantity,
            string? reason,
            Guid? batchId = null) =>
            StockMovement.Create(
                Guid.NewGuid(),
                ItemId,
                BranchId,
                batchId ?? Guid.Parse("11111111-1111-1111-1111-111111111111"),
                type,
                quantity,
                reason,
                "movement-key-1",
                Now,
                Actor);
    }
}
