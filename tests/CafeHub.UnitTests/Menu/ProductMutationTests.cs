using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu
{
    public sealed class ProductMutationTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public void Update_ChangesAttributesAndReportsNoChangeForIdenticalValues()
        {
            var product = Create();
            var categoryId = Guid.NewGuid();
            var stationId = Guid.NewGuid();

            var changed = product.Update(
                categoryId,
                stationId,
                "  Flat White ",
                "  Double shot ",
                "  Milk ",
                ProductKind.Food,
                false,
                7,
                4,
                Now.AddMinutes(1),
                Actor);

            Assert.True(changed);
            Assert.Equal(categoryId, product.CategoryId);
            Assert.Equal(stationId, product.StationId);
            Assert.Equal("Flat White", product.Name);
            Assert.Equal("Double shot", product.Description);
            Assert.Equal("Milk", product.Ingredients);
            Assert.Equal(ProductKind.Food, product.Kind);
            Assert.False(product.Caffeine);
            Assert.Equal(7, product.PreparationMinutes);
            Assert.Equal(4, product.SortOrder);
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);

            var unchanged = product.Update(
                categoryId,
                stationId,
                "Flat White",
                "Double shot",
                "Milk",
                ProductKind.Food,
                false,
                7,
                4,
                Now.AddMinutes(2),
                Actor);

            Assert.False(unchanged);
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);
        }

        [Fact]
        public void Update_RejectsInvalidValuesWithoutMutatingTheProduct()
        {
            var product = Create();

            Assert.Throws<ArgumentException>(() => product.Update(
                Guid.Empty, null, "Name", null, null, ProductKind.Drink, false, null, 0, Now, Actor));
            Assert.Throws<ArgumentOutOfRangeException>(() => product.Update(
                Guid.NewGuid(), null, "Name", null, null, (ProductKind)99, false, null, 0, Now, Actor));
            Assert.Throws<ArgumentOutOfRangeException>(() => product.Update(
                Guid.NewGuid(), null, "Name", null, null, ProductKind.Drink, false, 241, 0, Now, Actor));
            Assert.Throws<ArgumentException>(() => product.Update(
                Guid.NewGuid(), null, " ", null, null, ProductKind.Drink, false, null, 0, Now, Actor));
            Assert.Equal("Latte", product.Name);
        }

        [Fact]
        public void ReplaceSizes_UpdatesByIdCreatesNewAndDeactivatesMissingWithoutDeleting()
        {
            var small = Size("Small", 10m, isDefault: true);
            var medium = Size("Medium", 14m);
            var product = Create(sizes: [small, medium]);

            var changed = product.ReplaceSizes(
                [
                    new ProductSizeInput(small.Id, "Small", 11.50m, true),
                    new ProductSizeInput(null, "Large", 18m, false),
                ],
                Now.AddMinutes(1),
                Actor);

            Assert.True(changed);
            Assert.Equal(3, product.Sizes.Count);
            Assert.Equal(11.50m, small.Price);
            Assert.Equal(ProductComponentStatus.Inactive, medium.Status);
            Assert.Contains(medium, product.Sizes);
            var large = Assert.Single(product.Sizes, size => size.Name == "Large" && size.Status == ProductComponentStatus.Active);
            Assert.Equal(product.Id, large.ProductId);
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);
        }

        [Fact]
        public void ReplaceSizes_ReactivatesAnExistingSizeByIdAndKeepsItsIdentity()
        {
            var small = Size("Small", 10m, isDefault: true);
            var medium = Size("Medium", 14m);
            var product = Create(sizes: [small, medium]);
            product.ReplaceSizes([new ProductSizeInput(small.Id, "Small", 10m, true)], Now.AddMinutes(1), Actor);
            Assert.Equal(ProductComponentStatus.Inactive, medium.Status);

            var changed = product.ReplaceSizes(
                [
                    new ProductSizeInput(small.Id, "Small", 10m, true),
                    new ProductSizeInput(medium.Id, "Medium", 15m, false),
                ],
                Now.AddMinutes(2),
                Actor);

            Assert.True(changed);
            Assert.Equal(ProductComponentStatus.Active, medium.Status);
            Assert.Equal(15m, medium.Price);
            Assert.Equal(2, product.Sizes.Count);
        }

        [Fact]
        public void ReplaceSizes_RejectsEmptyForeignDuplicateAndDefaultViolations()
        {
            var small = Size("Small", 10m, isDefault: true);
            var product = Create(sizes: [small]);

            Assert.Throws<ArgumentException>(() => product.ReplaceSizes([], Now, Actor));
            Assert.Throws<ArgumentException>(() => product.ReplaceSizes(
                [new ProductSizeInput(Guid.NewGuid(), "Small", 10m, true)], Now, Actor));
            Assert.Throws<ArgumentException>(() => product.ReplaceSizes(
                [new ProductSizeInput(null, "Small", 10m, true)], Now, Actor));
            Assert.Throws<ArgumentException>(() => product.ReplaceSizes(
                [new ProductSizeInput(small.Id, "Small", 10m, false)], Now, Actor));
            Assert.Throws<ArgumentException>(() => product.ReplaceSizes(
                [
                    new ProductSizeInput(null, "Large", 10m, true),
                    new ProductSizeInput(null, " large ", 10m, false),
                ],
                Now,
                Actor));
            Assert.Throws<ArgumentOutOfRangeException>(() => product.ReplaceSizes(
                [new ProductSizeInput(small.Id, "Small", 10.001m, true)], Now, Actor));
        }

        [Fact]
        public void ReplaceAddons_UpdatesByIdCreatesNewAndDeactivatesMissingWithoutDeleting()
        {
            var milk = Addon("Milk", 2m);
            var syrup = Addon("Syrup", 3m);
            var product = Create(addons: [milk, syrup]);

            var changed = product.ReplaceAddons(
                [
                    new ProductAddonInput(milk.Id, "Milk", 2.50m),
                    new ProductAddonInput(null, "Extra shot", 5m),
                ],
                Now.AddMinutes(1),
                Actor);

            Assert.True(changed);
            Assert.Equal(3, product.Addons.Count);
            Assert.Equal(2.50m, milk.Price);
            Assert.Equal(ProductComponentStatus.Inactive, syrup.Status);
            Assert.Contains(syrup, product.Addons);
            Assert.Contains(product.Addons, addon => addon.Name == "Extra shot" && addon.ProductId == product.Id);
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);
        }

        [Fact]
        public void ReplaceAddons_RejectsForeignAndDuplicateEntries()
        {
            var product = Create(addons: [Addon("Milk", 2m)]);

            Assert.Throws<ArgumentException>(() => product.ReplaceAddons(
                [new ProductAddonInput(Guid.NewGuid(), "Milk", 2m)], Now, Actor));
            Assert.Throws<ArgumentException>(() => product.ReplaceAddons(
                [new ProductAddonInput(null, "Milk", 2m), new ProductAddonInput(null, " MILK ", 3m)], Now, Actor));
            Assert.Throws<ArgumentOutOfRangeException>(() => product.ReplaceAddons(
                [new ProductAddonInput(null, "Milk", -1m)], Now, Actor));
        }

        [Fact]
        public void ReplaceTags_NormalizesSortsAndOnlyTouchesWhenChanged()
        {
            var product = Create(tags: ["hot", "dairy"]);

            var unchanged = product.ReplaceTags(["DAIRY", " Hot "], Now.AddMinutes(1), Actor);
            Assert.False(unchanged);
            Assert.Equal(Now, product.UpdatedAt);

            var changed = product.ReplaceTags([" Hot ", "new"], Now.AddMinutes(2), Actor);
            Assert.True(changed);
            Assert.Equal(new[] { "hot", "new" }, product.Tags.Select(tag => tag.Tag).OrderBy(tag => tag));
            Assert.Equal(Now.AddMinutes(2), product.UpdatedAt);

            Assert.Throws<ArgumentException>(() => product.ReplaceTags(["hot", "HOT"], Now, Actor));
        }

        [Fact]
        public void ChangeStatus_IsIdempotentAndNeverDeletesComponents()
        {
            var product = Create(addons: [Addon("Milk", 2m)], tags: ["hot"]);

            Assert.True(product.ChangeStatus(ProductStatus.Inactive, Now.AddMinutes(1), Actor));
            Assert.Equal(ProductStatus.Inactive, product.Status);
            Assert.Single(product.Sizes);
            Assert.Single(product.Addons);
            Assert.Single(product.Tags);

            Assert.False(product.ChangeStatus(ProductStatus.Inactive, Now.AddMinutes(2), Actor));
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);

            Assert.True(product.ChangeStatus(ProductStatus.Active, Now.AddMinutes(3), Actor));
            Assert.Equal(ProductStatus.Active, product.Status);
            Assert.Single(product.Addons);
            Assert.Single(product.Tags);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => product.ChangeStatus((ProductStatus)99, Now, Actor));
        }

        private static ProductSize Size(string name, decimal price, bool isDefault = false) =>
            ProductSize.Create(Guid.CreateVersion7(), name, price, isDefault, Now, Actor);

        private static ProductAddon Addon(string name, decimal price) =>
            ProductAddon.Create(Guid.CreateVersion7(), name, price, Now, Actor);

        private static Product Create(
            IReadOnlyCollection<ProductSize>? sizes = null,
            IReadOnlyCollection<ProductAddon>? addons = null,
            IEnumerable<string>? tags = null) =>
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
                sizes ?? [Size("Regular", 10m, isDefault: true)],
                addons ?? [],
                tags ?? [],
                Now,
                Actor);
    }
}
