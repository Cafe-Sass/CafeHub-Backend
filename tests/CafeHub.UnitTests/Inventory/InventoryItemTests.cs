using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Inventory
{
    public sealed class InventoryItemTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 25, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public void Creation_SetsActiveStatusAndTrimsTheName()
        {
            var item = Create("  Coffee Beans  ");

            Assert.Equal("Coffee Beans", item.Name);
            Assert.Equal(InventoryUnit.Kg, item.Unit);
            Assert.Equal(InventoryItemStatus.Active, item.Status);
            Assert.Equal(Actor, item.CreatedBy);
            Assert.Equal(Actor, item.UpdatedBy);
            Assert.Equal(Now, item.CreatedAt);
            Assert.Equal(Now, item.UpdatedAt);
            Assert.NotEqual(Guid.Empty, item.Id);
        }

        [Fact]
        public void Names_AcceptTheExactBoundaryAndRejectOutOfRangeValues()
        {
            var boundary = new string('a', InventoryItem.MaximumNameLength);

            Assert.True(InventoryItem.IsValidName($"  {boundary}  "));
            Assert.False(InventoryItem.IsValidName(boundary + "a"));
            Assert.False(InventoryItem.IsValidName(string.Empty));
            Assert.False(InventoryItem.IsValidName("   "));
            Assert.False(InventoryItem.IsValidName(null));
            Assert.False(InventoryItem.IsValidName("Coffee\u0000Beans"));
            Assert.False(InventoryItem.IsValidName("Coffee\tBeans"));
            Assert.Throws<ArgumentException>(() => Create(boundary + "a"));
        }

        [Theory]
        [InlineData(InventoryUnit.Kg)]
        [InlineData(InventoryUnit.G)]
        [InlineData(InventoryUnit.L)]
        [InlineData(InventoryUnit.Ml)]
        [InlineData(InventoryUnit.Piece)]
        [InlineData(InventoryUnit.Pack)]
        public void Units_AcceptEveryMemberOfTheClosedSet(InventoryUnit unit)
        {
            Assert.True(InventoryItem.IsValidUnit(unit));
            Assert.Equal(unit, Create("Milk", unit).Unit);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(6)]
        [InlineData(99)]
        public void Units_RefuseValuesOutsideTheClosedSet(int raw)
        {
            var unit = (InventoryUnit)raw;

            Assert.False(InventoryItem.IsValidUnit(unit));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create("Milk", unit));
        }

        [Theory]
        [InlineData("0", true)]
        [InlineData("0.001", true)]
        [InlineData("1.234", true)]
        [InlineData("999999999.999", true)]
        [InlineData("-1", false)]
        [InlineData("0.0001", false)]
        [InlineData("1000000000", false)]
        public void LowStockThreshold_IsNonNegativeAndKeepsThreeDecimals(string raw, bool valid)
        {
            var threshold = decimal.Parse(raw, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(valid, InventoryItem.IsValidLowStockThreshold(threshold));
            if (!valid)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Create("Sugar", threshold: threshold));
            }
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(7, true)]
        [InlineData(365, true)]
        [InlineData(-1, false)]
        public void NearExpiryDays_RefuseNegativeValues(int days, bool valid)
        {
            Assert.Equal(valid, InventoryItem.IsValidNearExpiryDays(days));
            if (!valid)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Create("Yoghurt", nearExpiryDays: days));
            }
        }

        [Fact]
        public void NearExpiryDays_DefaultIsSevenDaysAndOmittedValidationAllowsNull()
        {
            Assert.Equal(7, InventoryItem.DefaultNearExpiryDays);
            Assert.True(InventoryItem.IsValidNearExpiryDays(null));
            Assert.False(InventoryItem.IsValidNearExpiryDays(-1));
        }

        [Fact]
        public void Update_ReturnsFalseWithoutTouchingWhenNothingChanged()
        {
            var item = Create("Coffee Beans");
            var updatedAt = Now.AddHours(1);

            Assert.False(item.Update(
                " Coffee Beans ", InventoryUnit.Kg, 5m, true, 7, updatedAt, Guid.NewGuid()));
            Assert.Equal(Now, item.UpdatedAt);
            Assert.Equal(Actor, item.UpdatedBy);

            var newActor = Guid.NewGuid();
            Assert.True(item.Update(
                "Coffee Beans Dark", InventoryUnit.G, 250m, false, 14, updatedAt, newActor));
            Assert.Equal("Coffee Beans Dark", item.Name);
            Assert.Equal(InventoryUnit.G, item.Unit);
            Assert.Equal(250m, item.LowStockThreshold);
            Assert.False(item.TracksExpiry);
            Assert.Equal(14, item.NearExpiryDays);
            Assert.Equal(updatedAt, item.UpdatedAt);
            Assert.Equal(newActor, item.UpdatedBy);
            Assert.Equal(Now, item.CreatedAt);
        }

        [Fact]
        public void Lifecycle_DeactivationKeepsTheRowAndReactivationRestoresIt()
        {
            var item = Create("Coffee Beans");

            Assert.True(item.Deactivate(Now.AddMinutes(1), Actor));
            Assert.Equal(InventoryItemStatus.Inactive, item.Status);
            Assert.Equal("Coffee Beans", item.Name);
            Assert.Equal(Now, item.CreatedAt);
            Assert.False(item.Deactivate(Now.AddMinutes(2), Actor));

            Assert.True(item.Reactivate(Now.AddMinutes(3), Actor));
            Assert.Equal(InventoryItemStatus.Active, item.Status);
            Assert.False(item.Reactivate(Now.AddMinutes(4), Actor));
            Assert.Equal(Now, item.CreatedAt);
        }

        [Fact]
        public void FormatQuantity_UsesInvariantFixedPrecisionText()
        {
            Assert.Equal("12.5", InventoryItem.FormatQuantity(12.5m));
            Assert.Equal("0.001", InventoryItem.FormatQuantity(0.001m));
            Assert.Equal("1000", InventoryItem.FormatQuantity(1000m));
        }

        private static InventoryItem Create(
            string name,
            InventoryUnit unit = InventoryUnit.Kg,
            decimal threshold = 5m,
            int nearExpiryDays = InventoryItem.DefaultNearExpiryDays) =>
            InventoryItem.Create(
                Guid.CreateVersion7(), name, unit, threshold, true, nearExpiryDays, Now, Actor);
    }
}
