using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Discovery;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Discovery;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Discovery
{
    public sealed class ProductDiscoveryGeneratorTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherBranchId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid LatteId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid MochaId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid TeaId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private const string Text = "something cold and sweet";

        [Fact]
        public async Task Discover_ReturnsTheGroundedProviderOrderWithTheAiSource()
        {
            var store = new FakeGuestMenuStore
            {
                Visible = [Latte(), Mocha(), Tea()],
                SearchResults = [Tea()],
            };
            var provider = new FakeProductDiscoveryProvider
            {
                Response = Result.Success<IReadOnlyList<ProductDiscoverySuggestion>>(
                [
                    new(TeaId, "Refreshing"),
                    new(LatteId, null),
                ]),
            };
            var generator = CreateGenerator(provider, store);

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Ai, result.Source);
            Assert.Equal(new[] { TeaId, LatteId }, result.Items.Select(item => item.Id).ToArray());
            Assert.Equal("Refreshing", result.Items[0].Reason);
            Assert.Equal(12m, result.Items[0].PriceFrom);
            Assert.Empty(store.Searches);
        }

        [Fact]
        public async Task Discover_SendsOnlyTheTextAndTheVisibleProductIdsAndNames()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte(), Mocha()] };
            var provider = new FakeProductDiscoveryProvider();
            var generator = CreateGenerator(provider, store);

            await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            var input = Assert.IsType<ProductDiscoveryInput>(provider.LastInput);
            Assert.Equal(Text, input.Text);
            Assert.Equal(
                new[] { new ProductDiscoveryCandidate(LatteId, "Latte"), new ProductDiscoveryCandidate(MochaId, "Mocha") },
                input.Products);
            Assert.Equal(BranchId, Assert.Single(store.ListedBranchIds));
        }

        [Fact]
        public async Task Discover_ASuggestedIdOutsideTheBranchFallsBackToTheKeywordSearch()
        {
            var store = new FakeGuestMenuStore
            {
                Visible = [Latte()],
                SearchResults = [Mocha()],
            };
            var provider = new FakeProductDiscoveryProvider
            {
                Response = Result.Success<IReadOnlyList<ProductDiscoverySuggestion>>(
                    [new(Guid.NewGuid(), "From another branch")]),
            };
            var generator = CreateGenerator(provider, store);

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Search, result.Source);
            Assert.Equal(MochaId, Assert.Single(result.Items).Id);
            Assert.Null(result.Items[0].Reason);
            Assert.Equal(Text, Assert.Single(store.Searches));
            Assert.Equal(ProductDiscoveryGrounding.MaximumResults, Assert.Single(store.SearchLimits));
        }

        [Fact]
        public async Task Discover_AnUnavailableProviderFallsBackToTheKeywordSearch()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [Tea()] };
            var provider = new FakeProductDiscoveryProvider();
            var generator = CreateGenerator(provider, store);

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Search, result.Source);
            Assert.Equal(TeaId, Assert.Single(result.Items).Id);
        }

        [Fact]
        public async Task Discover_NotConfiguredProviderUsesTheKeywordSearch()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [Tea()] };
            var generator = new ProductDiscoveryGenerator(
                new NotConfiguredProductDiscoveryProvider(),
                store,
                new CapturingLogger<ProductDiscoveryGenerator>());

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Search, result.Source);
            Assert.Equal(TeaId, Assert.Single(result.Items).Id);
        }

        [Fact]
        public async Task Discover_AProviderFailureFallsBackToTheKeywordSearch()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [Tea()] };
            var provider = new FakeProductDiscoveryProvider
            {
                Response = Result.Failure<IReadOnlyList<ProductDiscoverySuggestion>>(
                    Error.Failure("Discovery.ProviderUnavailable", "The provider is unavailable.")),
            };
            var generator = CreateGenerator(provider, store);

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Search, result.Source);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task Discover_AProviderExceptionFallsBackToTheKeywordSearch()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [Tea()] };
            var provider = new FakeProductDiscoveryProvider
            {
                Throws = new HttpRequestException("The provider is unreachable."),
            };
            var generator = CreateGenerator(provider, store);

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Search, result.Source);
            Assert.Single(result.Items);
        }

        [Fact]
        public async Task Discover_AnEmptySearchResultStaysASearchResponse()
        {
            var store = new FakeGuestMenuStore { Visible = [], SearchResults = [] };
            var generator = CreateGenerator(new FakeProductDiscoveryProvider(), store);

            var result = await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.Equal(ProductDiscoverySources.Search, result.Source);
            Assert.Empty(result.Items);
        }

        [Fact]
        public async Task Discover_PropagatesCancellationInsteadOfFallingBack()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [Tea()] };
            var provider = new FakeProductDiscoveryProvider { Throws = new OperationCanceledException() };
            var generator = CreateGenerator(provider, store);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(
                () => generator.DiscoverAsync(BranchId, Text, cancellation.Token));

            Assert.Empty(store.Searches);
        }

        [Fact]
        public async Task Discover_UsesTheResolvedBranchForTheFallbackSearch()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [] };
            var generator = CreateGenerator(new FakeProductDiscoveryProvider(), store);

            await generator.DiscoverAsync(OtherBranchId, Text, CancellationToken.None);

            Assert.Equal(OtherBranchId, Assert.Single(store.ListedBranchIds));
            Assert.Equal(OtherBranchId, Assert.Single(store.SearchedBranchIds));
        }

        [Fact]
        public async Task Discover_TheGuestTextIsNeverWrittenToTheLogs()
        {
            var store = new FakeGuestMenuStore { Visible = [Latte()], SearchResults = [Tea()] };
            var provider = new FakeProductDiscoveryProvider { Throws = new HttpRequestException(Text) };
            var logger = new CapturingLogger<ProductDiscoveryGenerator>();
            var generator = new ProductDiscoveryGenerator(provider, store, logger);

            await generator.DiscoverAsync(BranchId, Text, CancellationToken.None);

            Assert.NotEmpty(logger.Messages);
            Assert.All(logger.Messages, message =>
                Assert.DoesNotContain(Text, message, StringComparison.Ordinal));
        }

        private static ProductDiscoveryGenerator CreateGenerator(
            IProductDiscoveryProvider provider,
            FakeGuestMenuStore store) =>
            new(provider, store, new CapturingLogger<ProductDiscoveryGenerator>());

        private static GuestMenuProductSummary Latte() => new(LatteId, "Latte", 25.50m, 32m);

        private static GuestMenuProductSummary Mocha() => new(MochaId, "Mocha", 30m, 36m);

        private static GuestMenuProductSummary Tea() => new(TeaId, "Tea", 12m, 12m);

        private sealed class FakeProductDiscoveryProvider : IProductDiscoveryProvider
        {
            public Result<IReadOnlyList<ProductDiscoverySuggestion>> Response { get; init; } =
                Result.Success<IReadOnlyList<ProductDiscoverySuggestion>>(
                    Array.Empty<ProductDiscoverySuggestion>());

            public Exception? Throws { get; init; }

            public ProductDiscoveryInput? LastInput { get; private set; }

            public Task<Result<IReadOnlyList<ProductDiscoverySuggestion>>> SuggestAsync(
                ProductDiscoveryInput input,
                CancellationToken cancellationToken)
            {
                LastInput = input;
                if (Throws is not null)
                {
                    throw Throws;
                }

                return Task.FromResult(Response);
            }
        }

        private sealed class FakeGuestMenuStore : IGuestMenuStore
        {
            public IReadOnlyList<GuestMenuProductSummary> Visible { get; init; } = [];

            public IReadOnlyList<GuestMenuProductSummary> SearchResults { get; init; } = [];

            public List<Guid> ListedBranchIds { get; } = [];

            public List<Guid> SearchedBranchIds { get; } = [];

            public List<string> Searches { get; } = [];

            public List<int> SearchLimits { get; } = [];

            public Task<OffsetPage<GuestMenuItemResponse>> ListAsync(
                Guid branchId,
                string? search,
                Guid? categoryId,
                ProductKind? kind,
                bool? caffeine,
                string? tag,
                int page,
                int pageSize,
                IReadOnlyList<SortTerm> sort,
                bool includeTotalCount,
                CancellationToken cancellationToken) =>
                Task.FromResult(OffsetPage<GuestMenuItemResponse>.Create([], page, pageSize, null));

            public Task<IReadOnlyList<GuestMenuProductSummary>> ListVisibleAsync(
                Guid branchId,
                CancellationToken cancellationToken)
            {
                ListedBranchIds.Add(branchId);
                return Task.FromResult(Visible);
            }

            public Task<IReadOnlyList<GuestMenuProductSummary>> SearchVisibleAsync(
                Guid branchId,
                string search,
                int limit,
                CancellationToken cancellationToken)
            {
                SearchedBranchIds.Add(branchId);
                Searches.Add(search);
                SearchLimits.Add(limit);
                return Task.FromResult(SearchResults);
            }
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                Messages.Add(formatter(state, exception));
        }
    }
}
