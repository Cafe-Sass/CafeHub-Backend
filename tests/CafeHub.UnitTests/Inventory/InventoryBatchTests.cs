using System.Globalization;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryBatchTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        private static readonly Guid BranchId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        private static readonly Guid ItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        private static readonly DateOnly ReceivedDate = new(2026, 11, 25);

        [Fact]
        public void Creation_StartsRemainingAtTheReceivedQuantityAndKeepsTheReceiver()
        {
            var batch = Create(quantity: 12.5m, expiryDate: new DateOnly(2026, 12, 24));

            Assert.Equal(12.5m, batch.ReceivedQuantity);
            Assert.Equal(12.5m, batch.RemainingQuantity);
            Assert.Equal(ReceivedDate, batch.ReceivedDate);
            Assert.Equal(new DateOnly(2026, 12, 24), batch.ExpiryDate);
            Assert.Equal(Actor, batch.ReceivedByUserId);
            Assert.Equal(Actor, batch.CreatedBy);
            Assert.Equal(Actor, batch.UpdatedBy);
            Assert.Equal(Now, batch.CreatedAt);
            Assert.Equal(Now, batch.UpdatedAt);
            Assert.Equal("receipt-key-1", batch.IdempotencyKey);
            Assert.NotEqual(Guid.Empty, batch.Id);
        }

        [Fact]
        public void Creation_ForAnItemWithoutExpiryStoresNoExpiryDate()
        {
            var batch = Create(expiryDate: null, tracksExpiry: false);

            Assert.Null(batch.ExpiryDate);
            Assert.Throws<ArgumentException>(
                () => Create(expiryDate: new DateOnly(2026, 12, 1), tracksExpiry: false));
        }

        [Fact]
        public void Expiry_IsRequiredForAnItemThatTracksExpiry()
        {
            Assert.Throws<ArgumentException>(() => Create(expiryDate: null, tracksExpiry: true));
        }

        [Fact]
        public void Expiry_MayEqualTheReceivedDateButNeverPrecedeIt()
        {
            var sameDay = Create(expiryDate: ReceivedDate);

            Assert.Equal(ReceivedDate, sameDay.ExpiryDate);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Create(expiryDate: ReceivedDate.AddDays(-1)));
        }

        [Theory]
        [InlineData("0.001", true)]
        [InlineData("1.234", true)]
        [InlineData("999999999.999", true)]
        [InlineData("0", false)]
        [InlineData("-1", false)]
        [InlineData("0.0001", false)]
        [InlineData("1000000000", false)]
        public void Quantities_ArePositiveAndKeepThreeDecimals(string raw, bool valid)
        {
            var quantity = decimal.Parse(raw, CultureInfo.InvariantCulture);

            Assert.Equal(valid, InventoryBatch.IsValidQuantity(quantity));
            if (!valid)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Create(quantity: quantity));
            }
        }

        [Fact]
        public void ReceivedDate_IsRequired()
        {
            Assert.True(InventoryBatch.IsValidReceivedDate(ReceivedDate));
            Assert.False(InventoryBatch.IsValidReceivedDate(default));
            Assert.Throws<ArgumentException>(() => Create(receivedDate: default));
        }

        [Fact]
        public void Identifiers_AreRequired()
        {
            Assert.Throws<ArgumentException>(() => InventoryBatch.Create(
                Guid.Empty, ItemId, BranchId, 1m, ReceivedDate, null, false,
                Actor, "receipt-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => InventoryBatch.Create(
                Guid.CreateVersion7(), Guid.Empty, BranchId, 1m, ReceivedDate, null, false,
                Actor, "receipt-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => InventoryBatch.Create(
                Guid.CreateVersion7(), ItemId, Guid.Empty, 1m, ReceivedDate, null, false,
                Actor, "receipt-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => InventoryBatch.Create(
                Guid.CreateVersion7(), ItemId, BranchId, 1m, ReceivedDate, null, false,
                Guid.Empty, "receipt-key-1", Now, Actor));
            Assert.Throws<ArgumentException>(() => InventoryBatch.Create(
                Guid.CreateVersion7(), ItemId, BranchId, 1m, ReceivedDate, null, false,
                Actor, " ", Now, Actor));
        }

        [Fact]
        public void FormatDate_UsesInvariantCalendarText()
        {
            Assert.Equal("2026-11-25", InventoryBatch.FormatDate(ReceivedDate));
        }

        private static InventoryBatch Create(
            decimal quantity = 1m,
            DateOnly receivedDate = default,
            DateOnly? expiryDate = null,
            bool tracksExpiry = true) =>
            InventoryBatch.Create(
                Guid.CreateVersion7(),
                ItemId,
                BranchId,
                quantity,
                receivedDate == default ? ReceivedDate : receivedDate,
                expiryDate,
                tracksExpiry,
                Actor,
                "receipt-key-1",
                Now,
                Actor);
    }
}
