using CafeHub.Application.Menu.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Menu;

namespace CafeHub.UnitTests.Menu.Persistence
{
    public sealed class ProductAuditTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public void ProductCreate_SnapshotsAttributesAndEveryInitialPrice()
        {
            var size = ProductSize.Create(Guid.NewGuid(), "Regular", 10.50m, true, Now, Actor);
            var addon = ProductAddon.Create(Guid.NewGuid(), "Milk", 2m, Now, Actor);
            var product = CreateProduct(sizes: [size], addons: [addon]);

            var changes = TenantAuditChangeSets.ProductCreate(product);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Null(snapshot.BeforeState);
            Assert.Contains("\"Name\":\"Latte\"", snapshot.AfterState);
            Assert.Contains("\"Kind\":\"Drink\"", snapshot.AfterState);
            Assert.Contains("\"Caffeine\":\"True\"", snapshot.AfterState);
            Assert.Contains("\"Status\":\"Active\"", snapshot.AfterState);
            Assert.Contains("\"price.Regular\":\"10.50\"", snapshot.AfterState);
            Assert.Contains("\"price.Milk\":\"2\"", snapshot.AfterState);
        }

        [Fact]
        public void ProductUpdate_RecordsOnlyChangedPricesWithTheirOldAndNewValues()
        {
            var sizeId = Guid.NewGuid();
            var addonId = Guid.NewGuid();
            var size = ProductSize.Create(sizeId, "Small", 10m, true, Now, Actor);
            var addon = ProductAddon.Create(addonId, "Milk", 2m, Now, Actor);
            var product = CreateProduct(sizes: [size], addons: [addon]);
            var model = new ProductWriteModel(
                product.CategoryId,
                product.StationId,
                "Flat White",
                "Double shot",
                null,
                ProductKind.Drink,
                true,
                5,
                2,
                [
                    new ProductSizeInput(sizeId, "Small", 12.50m, true),
                    new ProductSizeInput(null, "Large", 18m, false),
                ],
                [
                    new ProductAddonInput(addonId, "Milk", 2m),
                    new ProductAddonInput(null, "Shot", 5m),
                ],
                []);

            product.Update(
                product.CategoryId,
                product.StationId,
                model.Name,
                model.Description,
                model.Ingredients,
                model.Kind,
                model.Caffeine,
                model.PreparationMinutes,
                model.SortOrder,
                Now.AddMinutes(1),
                Actor);
            var prices = ProductStore.PriceChanges(
                model,
                new Dictionary<Guid, decimal> { [sizeId] = 10m },
                new Dictionary<Guid, decimal> { [addonId] = 2m });
            var changes = TenantAuditChangeSets.ProductUpdate(
                product,
                "Latte",
                ProductKind.Drink,
                true,
                null,
                product.CategoryId,
                product.StationId,
                prices);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Contains("\"Name\":\"Latte\"", snapshot.BeforeState);
            Assert.Contains("\"Name\":\"Flat White\"", snapshot.AfterState);
            Assert.Contains("\"PreparationMinutes\":\"5\"", snapshot.AfterState);
            Assert.Contains("\"price.Small\":\"10\"", snapshot.BeforeState);
            Assert.Contains("\"price.Small\":\"12.50\"", snapshot.AfterState);
            Assert.Contains("\"price.Large\":\"18\"", snapshot.AfterState);
            Assert.Contains("\"price.Shot\":\"5\"", snapshot.AfterState);
            Assert.DoesNotContain("price.Milk", snapshot.BeforeState);
            Assert.DoesNotContain("price.Milk", snapshot.AfterState);
        }

        [Fact]
        public void ProductUpdate_RecordsNothingWhenPricesAndAttributesAreUnchanged()
        {
            var sizeId = Guid.NewGuid();
            var product = CreateProduct(sizes: [ProductSize.Create(sizeId, "Regular", 10m, true, Now, Actor)]);
            var model = new ProductWriteModel(
                product.CategoryId,
                product.StationId,
                product.Name,
                product.Description,
                product.Ingredients,
                product.Kind,
                product.Caffeine,
                product.PreparationMinutes,
                product.SortOrder,
                [new ProductSizeInput(sizeId, "Regular", 10m, true)],
                [],
                []);

            var prices = ProductStore.PriceChanges(
                model,
                new Dictionary<Guid, decimal> { [sizeId] = 10m },
                new Dictionary<Guid, decimal>());
            var changes = TenantAuditChangeSets.ProductUpdate(
                product,
                product.Name,
                product.Kind,
                product.Caffeine,
                product.PreparationMinutes,
                product.CategoryId,
                product.StationId,
                prices);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Empty(prices);
            Assert.Null(snapshot.BeforeState);
            Assert.Null(snapshot.AfterState);
        }

        private static Product CreateProduct(
            IReadOnlyCollection<ProductSize> sizes,
            IReadOnlyCollection<ProductAddon>? addons = null) =>
            Product.Create(
                Guid.CreateVersion7(),
                Guid.NewGuid(),
                null,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                sizes,
                addons ?? [],
                [],
                Now,
                Actor);
    }
}
