using CafeHub.Application.Orders;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderRequestHashTests
    {
        private static readonly Guid TableId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AddonId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        [Fact]
        public void TheHashIsStableForTheSameNormalizedPayload()
        {
            var first = OrderRequestHash.Compute(TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line()]);
            var second = OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "  Sara ", [Line()]);

            Assert.Equal(first, second);
            Assert.Equal(64, first.Length);
            Assert.All(first, character => Assert.True(char.IsAsciiDigit(character) || character is >= 'a' and <= 'f'));
        }

        [Fact]
        public void ExpectedPricesAndTheDeviceIdAreNotPartOfTheHash()
        {
            var baseline = OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line()]);
            var withOtherExpectations = OrderRequestHash.Compute(
                TableId,
                OrderServiceType.DineIn,
                OrderSource.Guest,
                "Sara",
                [Line() with { ExpectedUnitPrice = 99m, ExpectedAddonPrices = [99m] }]);

            Assert.Equal(baseline, withOtherExpectations);
        }

        [Fact]
        public void ADifferentPayloadProducesADifferentHash()
        {
            var baseline = OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line()]);

            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.Takeaway, OrderSource.Guest, "Sara", [Line()]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Captain, "Sara", [Line()]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Omar", [Line()]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                Guid.NewGuid(), OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line()]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line() with { Quantity = 3 }]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line() with { SizeId = Guid.NewGuid() }]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line() with { ProductId = Guid.NewGuid() }]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line() with { AddonIds = [] }]));
            Assert.NotEqual(baseline, OrderRequestHash.Compute(
                TableId, OrderServiceType.DineIn, OrderSource.Guest, "Sara", [Line(), Line()]));
        }

        private static SubmitOrderLine Line() =>
            new(ProductId, SizeId, [AddonId], 2, 10m, [2m]);
    }
}
