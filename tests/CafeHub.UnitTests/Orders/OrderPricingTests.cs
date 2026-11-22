using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Orders;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderPricingTests
    {
        private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid SizeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid AddonId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid StationId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        [Fact]
        public void TheTotalIsRecomputedFromTheCatalogAndNotFromTheExpectedValues()
        {
            var priced = OrderPricing.Evaluate([Line(3, 10m, [2m])], Catalog(), 32m);

            Assert.True(priced.IsSuccess);
            var line = Assert.Single(priced.Value.Lines);
            Assert.Equal(3, line.Quantity);
            Assert.Equal(10m, line.UnitPrice);
            Assert.Equal(2m, Assert.Single(line.Addons).Price);
            Assert.Equal(32m, line.LineTotal);
            Assert.Equal(32m, priced.Value.TotalAmount);
        }

        [Fact]
        public void TheSnapshotCarriesTheProductSizeStationAndAddonLabels()
        {
            var line = Assert.Single(OrderPricing.Evaluate([Line(1, 10m, [2m])], Catalog(), 12m).Value.Lines);

            Assert.Equal(ProductId, line.ProductId);
            Assert.Equal(SizeId, line.SizeId);
            Assert.Equal(StationId, line.StationId);
            Assert.Equal("Latte", line.ProductName);
            Assert.Equal("Large", line.SizeName);
            Assert.Equal("Oat milk", Assert.Single(line.Addons).Name);
        }

        [Fact]
        public void AChangedSizePriceReturnsTheChangedItemsWithTheirCurrentPrices()
        {
            var result = OrderPricing.Evaluate([Line(1, 9m, [2m])], Catalog(), 11m);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.PriceChangedCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error).Details;
            var change = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<OrderPriceChange>>(
                details[OrdersErrors.ItemsExtension]));
            Assert.Equal(1, change.LineNumber);
            Assert.Equal(ProductId, change.ProductId);
            Assert.Equal(SizeId, change.SizeId);
            Assert.Equal(10m, change.UnitPrice);
            Assert.Equal(2m, Assert.Single(change.Addons).Price);
            Assert.Equal(12m, change.LineTotal);
            Assert.Equal(12m, details[OrdersErrors.TotalExtension]);
        }

        [Fact]
        public void AChangedAddonPriceIsDetected()
        {
            var result = OrderPricing.Evaluate([Line(1, 10m, [1.5m])], Catalog(), 11.5m);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.PriceChangedCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error).Details;
            var change = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<OrderPriceChange>>(
                details[OrdersErrors.ItemsExtension]));
            Assert.Equal(2m, Assert.Single(change.Addons).Price);
        }

        [Fact]
        public void AStaleExpectedTotalIsADetectedChangeEvenWhenEveryLineMatches()
        {
            var result = OrderPricing.Evaluate([Line(1, 10m, [2m])], Catalog(), 99m);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.PriceChangedCode, result.Error!.Code);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error).Details;
            Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<OrderPriceChange>>(
                details[OrdersErrors.ItemsExtension]));
            Assert.Equal(12m, details[OrdersErrors.TotalExtension]);
        }

        [Fact]
        public void AMissingExpectedTotalIsNotTreatedAsAChange()
        {
            Assert.True(OrderPricing.Evaluate([Line(1, 10m, [2m])], Catalog(), null).IsSuccess);
        }

        public static TheoryData<string> UnavailableReasons => ["inactive", "hidden", "unavailable", "missing"];

        [Theory]
        [MemberData(nameof(UnavailableReasons))]
        public void AnUnavailableProductIsReportedWithItsLineAndProduct(string reason)
        {
            var catalog = reason switch
            {
                "inactive" => Catalog(status: ProductStatus.Inactive),
                "hidden" => Catalog(isVisible: false),
                "unavailable" => Catalog(isAvailable: false),
                _ => new OrderCatalog([]),
            };

            var result = OrderPricing.Evaluate([Line(1, 10m, [2m])], catalog, 12m);

            Assert.True(result.IsFailure);
            Assert.Equal(OrdersErrors.ProductUnavailableCode, result.Error!.Code);
            Assert.Equal(ErrorType.Conflict, result.Error.Type);
            var details = Assert.IsAssignableFrom<IErrorDetails>(result.Error).Details;
            var item = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<OrderUnavailableItem>>(
                details[OrdersErrors.ItemsExtension]));
            Assert.Equal(1, item.LineNumber);
            Assert.Equal(ProductId, item.ProductId);
        }

        [Fact]
        public void AnInactiveOrForeignSizeIsReportedAsUnavailable()
        {
            var catalog = Catalog(sizes: [new OrderCatalogSize(Guid.NewGuid(), "Small", 8m)]);

            var result = OrderPricing.Evaluate([Line(1, 10m, [2m])], catalog, 12m);

            Assert.Equal(OrdersErrors.ProductUnavailableCode, result.Error!.Code);
        }

        [Fact]
        public void AnInactiveOrForeignAddonIsReportedAsUnavailable()
        {
            var catalog = Catalog(addons: [new OrderCatalogAddon(Guid.NewGuid(), "Syrup", 1m)]);

            var result = OrderPricing.Evaluate([Line(1, 10m, [1m])], catalog, 11m);

            Assert.Equal(OrdersErrors.ProductUnavailableCode, result.Error!.Code);
        }

        [Fact]
        public void UnavailableLinesWinOverPriceChanges()
        {
            var catalog = Catalog(status: ProductStatus.Inactive);

            var result = OrderPricing.Evaluate([Line(1, 1m, [0m])], catalog, 1m);

            Assert.Equal(OrdersErrors.ProductUnavailableCode, result.Error!.Code);
        }

        private static SubmitOrderLine Line(int quantity, decimal unitPrice, IReadOnlyList<decimal> addonPrices) =>
            new(ProductId, SizeId, addonPrices.Select(_ => AddonId).ToArray(), quantity, unitPrice, addonPrices);

        private static OrderCatalog Catalog(
            ProductStatus status = ProductStatus.Active,
            bool isVisible = true,
            bool isAvailable = true,
            IReadOnlyList<OrderCatalogSize>? sizes = null,
            IReadOnlyList<OrderCatalogAddon>? addons = null) =>
            new([
                new OrderCatalogProduct(
                    ProductId,
                    "Latte",
                    StationId,
                    status,
                    isVisible,
                    isAvailable,
                    sizes ?? [new OrderCatalogSize(SizeId, "Large", 10m)],
                    addons ?? [new OrderCatalogAddon(AddonId, "Oat milk", 2m)]),
            ]);
    }
}
