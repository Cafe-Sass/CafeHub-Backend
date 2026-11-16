using CafeHub.Application.Menu.Commands;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu.Commands
{
    public sealed class ProductCommandValidatorTests
    {
        private readonly CreateProductCommandValidator createValidator = new();
        private readonly UpdateProductCommandValidator updateValidator = new();
        private readonly DeactivateProductCommandValidator deactivateValidator = new();
        private readonly ReactivateProductCommandValidator reactivateValidator = new();

        [Fact]
        public void Create_WithCompletePayload_IsValid()
        {
            var result = createValidator.Validate(ProductCommandHandlerTests.CreateCommand());

            Assert.True(result.IsValid);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void Create_RequiresExactlyOneDefaultSize(int defaultCount)
        {
            var sizes = Enumerable.Range(0, 2)
                .Select(index => new ProductSizeInput(null, $"Size {index}", 10m, index < defaultCount))
                .ToArray();

            var result = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(sizes: sizes));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName == "Sizes");
        }

        [Fact]
        public void Create_RejectsEmptySizesDuplicateNamesAndProvidedIds()
        {
            var empty = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(sizes: []));
            var duplicate = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(sizes:
                [
                    new ProductSizeInput(null, "Regular", 10m, true),
                    new ProductSizeInput(null, " regular ", 11m, false),
                ]));
            var withId = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(sizes:
                [new ProductSizeInput(Guid.NewGuid(), "Regular", 10m, true)]));

            Assert.False(empty.IsValid);
            Assert.False(duplicate.IsValid);
            Assert.False(withId.IsValid);
            Assert.Contains(withId.Errors, failure => failure.PropertyName == "Sizes");
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("123456789012345678901234567890123")]
        public void Create_EnforcesSizeNameBoundaries(string name)
        {
            var result = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(sizes:
                [new ProductSizeInput(null, name, 10m, true)]));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName.StartsWith("Sizes[0].Name", StringComparison.Ordinal));
        }

        [Theory]
        [InlineData(-0.01)]
        [InlineData(1.001)]
        [InlineData(100000000)]
        public void Create_EnforcesSizePriceRangeAndScale(double price)
        {
            var result = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(sizes:
                [new ProductSizeInput(null, "Regular", (decimal)price, true)]));

            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, failure => failure.PropertyName.StartsWith("Sizes[0].Price", StringComparison.Ordinal));
        }

        [Fact]
        public void Create_RejectsAddonIdsDuplicateNamesAndInvalidPrices()
        {
            var withId = CreateWithAddons([new ProductAddonInput(Guid.NewGuid(), "Milk", 1m)]);
            var duplicate = CreateWithAddons(
                [new ProductAddonInput(null, "Milk", 1m), new ProductAddonInput(null, " milk ", 2m)]);
            var negative = CreateWithAddons([new ProductAddonInput(null, "Milk", -1m)]);

            Assert.False(createValidator.Validate(withId).IsValid);
            Assert.False(createValidator.Validate(duplicate).IsValid);
            Assert.False(createValidator.Validate(negative).IsValid);
        }

        [Fact]
        public void Create_EnforcesTagCountFormatAndUniqueness()
        {
            var tooMany = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(
                tags: Enumerable.Range(0, Product.MaximumTagCount + 1).Select(index => $"tag-{index}").ToArray()));
            var duplicate = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(tags: ["hot", "HOT"]));
            var invalid = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(tags: ["under_score"]));

            Assert.False(tooMany.IsValid);
            Assert.False(duplicate.IsValid);
            Assert.False(invalid.IsValid);
        }

        [Theory]
        [InlineData(96, true)]
        [InlineData(97, false)]
        public void Create_EnforcesTheNameLengthBoundary(int length, bool expected)
        {
            var result = createValidator.Validate(ProductCommandHandlerTests.CreateCommand(name: new string('a', length)));

            Assert.Equal(expected, result.IsValid);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(241)]
        public void Create_RejectsPreparationMinutesOutsideTheRange(int minutes)
        {
            var command = new CreateProductCommand(
                Guid.NewGuid(),
                null,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                minutes,
                0,
                [new ProductSizeInput(null, "Regular", 10m, true)],
                [],
                []);

            Assert.False(createValidator.Validate(command).IsValid);
        }

        [Fact]
        public void Update_WithValidCommandAndIdentifiers_IsValid()
        {
            var command = ProductCommandHandlerTests.UpdateCommand() with
            {
                Sizes =
                [
                    new ProductSizeInput(Guid.NewGuid(), "Regular", 10m, true),
                    new ProductSizeInput(null, "Large", 12m, false),
                ],
            };

            Assert.True(updateValidator.Validate(command).IsValid);
        }

        [Fact]
        public void Update_RequiresTheProductIdAndAValidToken()
        {
            var emptyId = ProductCommandHandlerTests.UpdateCommand() with { Id = Guid.Empty };
            var invalidToken = ProductCommandHandlerTests.UpdateCommand(concurrencyToken: "1.5");

            Assert.False(updateValidator.Validate(emptyId).IsValid);
            var tokenResult = updateValidator.Validate(invalidToken);
            Assert.False(tokenResult.IsValid);
            Assert.Contains(tokenResult.Errors, failure => failure.PropertyName == "ConcurrencyToken");
        }

        [Fact]
        public void StatusCommands_RequireAnIdAndAValidToken()
        {
            Assert.True(deactivateValidator.Validate(new DeactivateProductCommand(Guid.NewGuid(), "42")).IsValid);
            Assert.True(reactivateValidator.Validate(new ReactivateProductCommand(Guid.NewGuid(), "42")).IsValid);

            Assert.False(deactivateValidator.Validate(new DeactivateProductCommand(Guid.Empty, "42")).IsValid);
            Assert.False(reactivateValidator.Validate(new ReactivateProductCommand(Guid.NewGuid(), "x")).IsValid);
        }

        private static CreateProductCommand CreateWithAddons(IReadOnlyList<ProductAddonInput> addons) =>
            new(
                Guid.NewGuid(),
                null,
                "Latte",
                null,
                null,
                ProductKind.Drink,
                true,
                null,
                0,
                [new ProductSizeInput(null, "Regular", 10m, true)],
                addons,
                []);
    }
}
