using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu
{
    public sealed class ProductTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);
        private static readonly Guid Actor = Guid.NewGuid();

        [Fact]
        public void Create_NormalizesFieldsAndAssignsComponentsToTheProduct()
        {
            var size = Size(" Large ", 42.50m, isDefault: true);
            var addon = Addon(" Extra shot ", 7.25m);

            var product = Create(
                name: "  Latte ",
                description: "  Milk and espresso ",
                ingredients: "  Beans, milk ",
                sizes: [size],
                addons: [addon],
                tags: [" Hot ", "DAIRY"]);

            Assert.Equal("Latte", product.Name);
            Assert.Equal("Milk and espresso", product.Description);
            Assert.Equal("Beans, milk", product.Ingredients);
            Assert.Equal(ProductStatus.Active, product.Status);
            Assert.Equal(ProductKind.Drink, product.Kind);
            Assert.Equal(product.Id, size.ProductId);
            Assert.Equal(product.Id, addon.ProductId);
            Assert.Same(size, Assert.Single(product.Sizes));
            Assert.Same(addon, Assert.Single(product.Addons));
            Assert.Equal(new[] { "dairy", "hot" }, product.Tags.Select(tag => tag.Tag).OrderBy(tag => tag));
            Assert.All(product.Tags, tag => Assert.Equal(product.Id, tag.ProductId));
            Assert.Equal(Actor, product.CreatedBy);
            Assert.Equal(Now, product.UpdatedAt);
        }

        [Fact]
        public void Create_RejectsMissingIdentityWrongKindAndOutOfRangeOptionalValues()
        {
            Assert.Throws<ArgumentException>(() => Create(id: Guid.Empty));
            Assert.Throws<ArgumentException>(() => Create(categoryId: Guid.Empty));
            Assert.Throws<ArgumentException>(() => Create(stationId: Guid.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(kind: (ProductKind)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(preparationMinutes: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(preparationMinutes: 241));
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(sortOrder: -1));
        }

        [Fact]
        public void Create_RejectsInvalidOrMissingSizes()
        {
            Assert.Throws<ArgumentException>(() => Create(sizes: []));
            Assert.Throws<ArgumentException>(() => Create(sizes: [Size("One", 1m), Size("Two", 2m)]));
            Assert.Throws<ArgumentException>(() => Create(sizes: [Size("One", 1m, isDefault: true), Size("Two", 2m, isDefault: true)]));

            var size = Size("One", 1m, isDefault: true);
            Assert.Throws<ArgumentException>(() => Create(sizes: [size, size]));

            var inactive = Size("One", 1m, isDefault: true);
            inactive.Deactivate(Now, Actor);
            Assert.Throws<ArgumentException>(() => Create(sizes: [inactive]));
        }

        [Fact]
        public void Create_RejectsDuplicateActiveComponentNamesRegardlessOfCase()
        {
            Assert.Throws<ArgumentException>(() => Create(
                sizes: [Size("Large", 1m, isDefault: true), Size(" large ", 2m)]));
            Assert.Throws<ArgumentException>(() => Create(
                addons: [Addon("Milk", 1m), Addon("MILK ", 2m)]));
            Assert.Throws<ArgumentException>(() => Create(
                addons: [Addon("Milk", 1m), Addon("Milk", 2m)]));
        }

        [Fact]
        public void Create_RejectsInvalidTags()
        {
            Assert.Throws<ArgumentException>(() => Create(tags: ["hot", "HOT"]));
            Assert.Throws<ArgumentException>(() => Create(tags: ["hot", "hot "]));
            Assert.Throws<ArgumentException>(() => Create(tags: ["caf\u00e9"]));
            Assert.Throws<ArgumentException>(() => Create(tags: ["under_score"]));
            Assert.Throws<ArgumentException>(() => Create(tags: [" "]));
            Assert.Throws<ArgumentException>(() => Create(tags: [new string('a', 33)]));
            Assert.Throws<ArgumentException>(() => Create(tags: Enumerable.Range(0, 11).Select(index => $"tag-{index}")));
        }

        [Fact]
        public void Create_TrimsNamesAndTextAndEnforcesTheirLimits()
        {
            var product = Create(name: new string('a', Product.MaximumNameLength));
            Assert.Equal(Product.MaximumNameLength, product.Name.Length);

            Assert.Throws<ArgumentException>(() => Create(name: " "));
            Assert.Throws<ArgumentException>(() => Create(name: new string('a', Product.MaximumNameLength + 1)));
            Assert.Throws<ArgumentException>(() => Create(name: "Bad\0Name"));
            Assert.Throws<ArgumentException>(() => Create(description: new string('a', Product.MaximumDescriptionLength + 1)));
            Assert.Throws<ArgumentException>(() => Create(ingredients: new string('a', Product.MaximumIngredientsLength + 1)));

            var blank = Create(description: "  ", ingredients: null);
            Assert.Null(blank.Description);
            Assert.Null(blank.Ingredients);
        }

        [Fact]
        public void ComponentPrices_AcceptTheFullRangeAndRejectSubCentOrNegativeValues()
        {
            var free = Size("Free", 0m, isDefault: true);
            var maximum = Addon("Maximum", ProductAddon.MaximumPrice);
            Assert.Equal(0m, free.Price);
            Assert.Equal(ProductAddon.MaximumPrice, maximum.Price);

            Assert.Throws<ArgumentOutOfRangeException>(() => Size("Negative", -0.01m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Size("SubCent", 1.001m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Size("TooLarge", ProductSize.MaximumPrice + 0.01m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Addon("Negative", -0.01m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Addon("SubCent", 1.001m));
            Assert.Throws<ArgumentOutOfRangeException>(() => Addon("TooLarge", ProductAddon.MaximumPrice + 0.01m));
        }

        [Fact]
        public void ComponentNames_EnforceLengthsAndRejectControlCharacters()
        {
            Assert.Throws<ArgumentException>(() => Size(" ", 1m));
            Assert.Throws<ArgumentException>(() => Size(new string('a', ProductSize.MaximumNameLength + 1), 1m));
            Assert.Throws<ArgumentException>(() => Size("Bad\0Name", 1m));
            Assert.Throws<ArgumentException>(() => Addon(" ", 1m));
            Assert.Throws<ArgumentException>(() => Addon(new string('a', ProductAddon.MaximumNameLength + 1), 1m));
            Assert.Throws<ArgumentException>(() => Addon("Bad\0Name", 1m));

            Assert.Equal(ProductSize.MaximumNameLength, Size(new string('a', ProductSize.MaximumNameLength), 1m).Name.Length);
            Assert.Equal(ProductAddon.MaximumNameLength, Addon(new string('a', ProductAddon.MaximumNameLength), 1m).Name.Length);
        }

        [Fact]
        public void AddSize_EnforcesActiveNameAndDefaultUniquenessButAllowsReuseAfterDeactivation()
        {
            var product = Create(sizes: [Size("Small", 1m, isDefault: true)]);

            Assert.Throws<ArgumentException>(() => product.AddSize(Size(" small ", 2m), Now, Actor));
            Assert.Throws<ArgumentException>(() => product.AddSize(Size("Large", 2m, isDefault: true), Now, Actor));

            var large = Size("Large", 2m);
            Assert.True(product.AddSize(large, Now.AddMinutes(1), Actor));
            Assert.Equal(2, product.Sizes.Count);
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);

            Assert.True(product.DeactivateSize(large.Id, Now.AddMinutes(2), Actor));
            Assert.False(product.DeactivateSize(large.Id, Now.AddMinutes(2), Actor));
            Assert.Equal(ProductComponentStatus.Inactive, large.Status);
            Assert.True(product.AddSize(Size("Large", 3m), Now.AddMinutes(3), Actor));
        }

        [Fact]
        public void DeactivateSize_RejectsTheDefaultAndUnknownSizes()
        {
            var size = Size("Small", 1m, isDefault: true);
            var product = Create(sizes: [size]);

            Assert.Throws<InvalidOperationException>(() => product.DeactivateSize(size.Id, Now, Actor));
            Assert.Throws<ArgumentException>(() => product.DeactivateSize(Guid.NewGuid(), Now, Actor));
            Assert.Equal(ProductComponentStatus.Active, size.Status);
        }

        [Fact]
        public void AddAddon_EnforcesActiveNameUniquenessAndRejectsInactiveAddons()
        {
            var product = Create(addons: [Addon("Milk", 1m)]);

            Assert.Throws<ArgumentException>(() => product.AddAddon(Addon(" milk ", 2m), Now, Actor));

            var inactive = Addon("Extra", 2m);
            inactive.Deactivate(Now, Actor);
            Assert.Throws<ArgumentException>(() => product.AddAddon(inactive, Now, Actor));

            var extra = Addon("Extra", 2m);
            Assert.True(product.AddAddon(extra, Now.AddMinutes(1), Actor));
            Assert.Equal(2, product.Addons.Count);
            Assert.Equal(Now.AddMinutes(1), product.UpdatedAt);

            Assert.True(product.DeactivateAddon(extra.Id, Now.AddMinutes(2), Actor));
            Assert.False(product.DeactivateAddon(extra.Id, Now.AddMinutes(2), Actor));
            Assert.True(product.AddAddon(Addon("Extra", 3m), Now.AddMinutes(3), Actor));
            Assert.Throws<ArgumentException>(() => product.DeactivateAddon(Guid.NewGuid(), Now, Actor));
        }

        private static ProductSize Size(string name, decimal price, bool isDefault = false) =>
            ProductSize.Create(Guid.CreateVersion7(), name, price, isDefault, Now, Actor);

        private static ProductAddon Addon(string name, decimal price) =>
            ProductAddon.Create(Guid.CreateVersion7(), name, price, Now, Actor);

        private static Product Create(
            Guid? id = null,
            Guid? categoryId = null,
            Guid? stationId = null,
            string name = "Latte",
            string? description = null,
            string? ingredients = null,
            ProductKind kind = ProductKind.Drink,
            bool caffeine = true,
            int? preparationMinutes = null,
            int sortOrder = 0,
            IReadOnlyCollection<ProductSize>? sizes = null,
            IReadOnlyCollection<ProductAddon>? addons = null,
            IEnumerable<string>? tags = null) =>
            Product.Create(
                id ?? Guid.CreateVersion7(),
                categoryId ?? Guid.CreateVersion7(),
                stationId,
                name,
                description,
                ingredients,
                kind,
                caffeine,
                preparationMinutes,
                sortOrder,
                sizes ?? [Size("Regular", 10m, isDefault: true)],
                addons ?? [],
                tags ?? [],
                Now,
                Actor);
    }
}
