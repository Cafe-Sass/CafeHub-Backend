using CafeHub.Application.Menu.Contracts;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;

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
            var addon = ProductAddon.Create(Guid.NewGuid(), "Regular", 2m, Now, Actor);
            var product = CreateProduct(sizes: [size], addons: [addon]);

            var changes = TenantAuditChangeSets.ProductCreate(product);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Null(snapshot.BeforeState);
            Assert.Contains("\"Name\":\"Latte\"", snapshot.AfterState);
            Assert.Contains("\"Kind\":\"Drink\"", snapshot.AfterState);
            Assert.Contains("\"Caffeine\":\"True\"", snapshot.AfterState);
            Assert.Contains("\"Status\":\"Active\"", snapshot.AfterState);
            Assert.Contains($"\"price.size.{size.Id:D}\":\"10.50\"", snapshot.AfterState);
            Assert.Contains($"\"price.addon.{addon.Id:D}\":\"2\"", snapshot.AfterState);
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

            var before = TenantAuditChangeSets.ProductFields(product);
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
            product.ReplaceSizes(model.Sizes, Now.AddMinutes(1), Actor);
            product.ReplaceAddons(model.Addons, Now.AddMinutes(1), Actor);
            var changes = TenantAuditChangeSets.ProductUpdate(before, product);

            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Contains("\"Name\":\"Latte\"", snapshot.BeforeState);
            Assert.Contains("\"Name\":\"Flat White\"", snapshot.AfterState);
            Assert.Contains("\"PreparationMinutes\":\"5\"", snapshot.AfterState);
            Assert.Contains($"\"price.size.{sizeId:D}\":\"10\"", snapshot.BeforeState);
            Assert.Contains($"\"price.size.{sizeId:D}\":\"12.50\"", snapshot.AfterState);
            Assert.Contains($"\"price.size.{product.Sizes.Single(item => item.Name == "Large").Id:D}\":\"18\"", snapshot.AfterState);
            Assert.Contains($"\"price.addon.{product.Addons.Single(item => item.Name == "Shot").Id:D}\":\"5\"", snapshot.AfterState);
            Assert.DoesNotContain($"price.addon.{addonId:D}", snapshot.BeforeState);
            Assert.DoesNotContain($"price.addon.{addonId:D}", snapshot.AfterState);
        }

        [Fact]
        public void ProductUpdate_RecordsNothingWhenPricesAndAttributesAreUnchanged()
        {
            var sizeId = Guid.NewGuid();
            var product = CreateProduct(sizes: [ProductSize.Create(sizeId, "Regular", 10m, true, Now, Actor)]);
            var changes = TenantAuditChangeSets.ProductUpdate(TenantAuditChangeSets.ProductFields(product), product);
            var snapshot = TenantAuditSnapshotSerializer.Create("products", changes).Value;

            Assert.Empty(changes.Fields);
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
