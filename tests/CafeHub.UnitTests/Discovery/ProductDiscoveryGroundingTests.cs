using CafeHub.Application.Discovery;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.UnitTests.Discovery
{
    public sealed class ProductDiscoveryGroundingTests
    {
        private static readonly Guid LatteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid MochaId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid TeaId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid JuiceId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid CakeId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly Guid ToastId = Guid.Parse("66666666-6666-6666-6666-666666666666");

        [Fact]
        public void Ground_KeepsTheProviderOrderAndTheDatabaseNames()
        {
            var suggestions = new[]
            {
                new ProductDiscoverySuggestion(TeaId, "Light and warm"),
                new ProductDiscoverySuggestion(LatteId, null),
            };

            var items = ProductDiscoveryGrounding.Ground(Visible(), suggestions);

            Assert.Equal(new[] { TeaId, LatteId }, items.Select(item => item.Id).ToArray());
            Assert.Equal(new[] { "Tea", "Latte" }, items.Select(item => item.Name).ToArray());
            Assert.Equal("Light and warm", items[0].Reason);
            Assert.Null(items[1].Reason);
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("otherBranch")]
        public void Ground_DropsIdsThatAreNotVisibleAndAvailableInTheBranch(string suggestion)
        {
            var suggestedId = suggestion == "unknown" ? Guid.NewGuid() : ToastId;

            var items = ProductDiscoveryGrounding.Ground(
                Visible(),
                new[] { new ProductDiscoverySuggestion(suggestedId, "Suggested") });

            Assert.Empty(items);
        }

        [Fact]
        public void Ground_DropsAnUnavailableProductButKeepsTheNextVisibleSuggestion()
        {
            var items = ProductDiscoveryGrounding.Ground(
                Visible(),
                new[]
                {
                    new ProductDiscoverySuggestion(ToastId, "From another branch"),
                    new ProductDiscoverySuggestion(MochaId, "Chocolate"),
                });

            var item = Assert.Single(items);
            Assert.Equal(MochaId, item.Id);
            Assert.Equal("Chocolate", item.Reason);
        }

        [Fact]
        public void Ground_KeepsTheFirstSuggestionForADuplicateId()
        {
            var items = ProductDiscoveryGrounding.Ground(
                Visible(),
                new[]
                {
                    new ProductDiscoverySuggestion(LatteId, "First"),
                    new ProductDiscoverySuggestion(LatteId, "Second"),
                });

            Assert.Equal("First", Assert.Single(items).Reason);
        }

        [Fact]
        public void Ground_CapsTheResultsAtFive()
        {
            var products = Enumerable.Range(1, 8)
                .Select(index => new GuestMenuProductSummary(Guid.NewGuid(), $"Product {index}", 1m, 2m))
                .ToArray();
            var suggestions = products
                .Select(product => new ProductDiscoverySuggestion(product.Id, null))
                .ToArray();

            var items = ProductDiscoveryGrounding.Ground(products, suggestions);

            Assert.Equal(ProductDiscoveryGrounding.MaximumResults, items.Count);
            Assert.Equal(products.Take(5).Select(product => product.Id), items.Select(item => item.Id));
        }

        [Fact]
        public void Ground_UsesTheDatabasePriceRange()
        {
            var items = ProductDiscoveryGrounding.Ground(
                new[] { new GuestMenuProductSummary(LatteId, "Latte", 25.50m, 40m) },
                new[] { new ProductDiscoverySuggestion(LatteId, null) });

            var item = Assert.Single(items);
            Assert.Equal(25.50m, item.PriceFrom);
            Assert.Equal(40m, item.PriceTo);
        }

        [Fact]
        public void Ground_TrimsReasonsAndDropsBlankOnes()
        {
            var items = ProductDiscoveryGrounding.Ground(
                Visible(),
                new[]
                {
                    new ProductDiscoverySuggestion(LatteId, "  Milk and espresso  "),
                    new ProductDiscoverySuggestion(MochaId, "   "),
                });

            Assert.Equal("Milk and espresso", items[0].Reason);
            Assert.Null(items[1].Reason);
        }

        [Fact]
        public void Ground_TruncatesAReasonToTheMaximumLength()
        {
            var reason = new string('a', ProductDiscoveryGrounding.MaximumReasonLength + 40);

            var items = ProductDiscoveryGrounding.Ground(
                Visible(),
                new[] { new ProductDiscoverySuggestion(LatteId, reason) });

            Assert.Equal(ProductDiscoveryGrounding.MaximumReasonLength, Assert.Single(items).Reason!.Length);
        }

        [Fact]
        public void Ground_NullOrEmptySuggestionsYieldNoItems()
        {
            Assert.Empty(ProductDiscoveryGrounding.Ground(Visible(), null));
            Assert.Empty(ProductDiscoveryGrounding.Ground(Visible(), Array.Empty<ProductDiscoverySuggestion>()));
            Assert.Empty(ProductDiscoveryGrounding.Ground(
                Array.Empty<GuestMenuProductSummary>(),
                new[] { new ProductDiscoverySuggestion(LatteId, null) }));
        }

        [Fact]
        public void ToItems_KeepsAtMostFiveProductsWithoutReasons()
        {
            var products = Enumerable.Range(1, 7)
                .Select(index => new GuestMenuProductSummary(Guid.NewGuid(), $"Product {index}", 10m, 20m))
                .ToArray();

            var items = ProductDiscoveryGrounding.ToItems(products);

            Assert.Equal(ProductDiscoveryGrounding.MaximumResults, items.Count);
            Assert.All(items, item => Assert.Null(item.Reason));
            Assert.Equal(10m, items[0].PriceFrom);
        }

        private static IReadOnlyList<GuestMenuProductSummary> Visible() =>
        [
            new(LatteId, "Latte", 25.50m, 32m),
            new(MochaId, "Mocha", 30m, 36m),
            new(TeaId, "Tea", 12m, 12m),
            new(JuiceId, "Juice", null, null),
            new(CakeId, "Cake", 45m, 55m),
        ];
    }
}
