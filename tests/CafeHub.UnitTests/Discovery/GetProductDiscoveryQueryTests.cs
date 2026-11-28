using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Discovery;
using CafeHub.Application.Discovery.Queries;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Application.Tables;
using CafeHub.Application.Tables.DTOs;
using CafeHub.Application.Tables.Queries;
using CafeHub.Domain.Tenants;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeHub.UnitTests.Discovery
{
    public sealed class GetProductDiscoveryQueryTests
    {
        private static readonly Guid BranchId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid LatteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private const string Code = "Abcdefghijklmnopqrstuv";

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\r\n\t")]
        public void Validate_AnEmptyOrControlOnlyTextIsRefused(string? text)
        {
            var validation = new GetProductDiscoveryQueryValidator().Validate(
                new GetProductDiscoveryQuery { Query = text });

            Assert.False(validation.IsValid);
            var failure = Assert.Single(validation.Errors);
            Assert.Equal("query", failure.PropertyName);
            Assert.Equal("The query is required.", failure.ErrorMessage);
        }

        [Fact]
        public void Validate_TextLongerThanTheLimitIsRefused()
        {
            var text = new string('a', ProductDiscoveryText.MaximumLength + 1);

            var validation = new GetProductDiscoveryQueryValidator().Validate(
                new GetProductDiscoveryQuery { Query = text });

            Assert.False(validation.IsValid);
            Assert.Contains(
                validation.Errors,
                error => error.ErrorMessage == "The query must be at most 200 characters.");
        }

        [Fact]
        public void Validate_TextAtTheLimitIsAccepted()
        {
            var text = new string('a', ProductDiscoveryText.MaximumLength);

            var validation = new GetProductDiscoveryQueryValidator().Validate(
                new GetProductDiscoveryQuery { Query = text });

            Assert.True(validation.IsValid);
        }

        [Fact]
        public void Validate_ControlCharactersDoNotCountTowardsTheLimit()
        {
            var text = string.Concat(Enumerable.Repeat("a\r\n", ProductDiscoveryText.MaximumLength / 2));

            var validation = new GetProductDiscoveryQueryValidator().Validate(
                new GetProductDiscoveryQuery { Query = text });

            Assert.True(validation.IsValid);
        }

        [Fact]
        public void Normalize_TrimsAndRemovesControlCharacters()
        {
            Assert.Equal("cold coffee", ProductDiscoveryText.Normalize("  cold\r\n\t coffee  "));
            Assert.Equal("coldcoffee", ProductDiscoveryText.Normalize("cold\u0000coffee"));
            Assert.Equal(string.Empty, ProductDiscoveryText.Normalize(null));
            Assert.Equal(string.Empty, ProductDiscoveryText.Normalize(" \r\n\t"));
        }

        [Fact]
        public void TheQueryCarriesTheGuestTextAsASensitiveRequest()
        {
            Assert.IsAssignableFrom<ISensitiveRequest>(new GetProductDiscoveryQuery());
        }

        [Fact]
        public async Task Handle_ResolvesTheQrAndPassesTheBranchAndNormalizedTextToTheGenerator()
        {
            var store = new FakeGuestMenuStore
            {
                Visible = [new GuestMenuProductSummary(LatteId, "Latte", 20m, 25m)],
            };
            var provider = new FakeProvider
            {
                Response = Result.Success<IReadOnlyList<ProductDiscoverySuggestion>>(
                    [new(LatteId, "Milky")]),
            };
            var sender = new RecordingSender { Response = Result.Success(Context()) };
            var handler = new GetProductDiscoveryQueryHandler(sender, Generator(provider, store));

            var result = await handler.Handle(
                new GetProductDiscoveryQuery { Code = Code, Query = "  something milky\r\n" },
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(ProductDiscoverySources.Ai, result.Value!.Source);
            var item = Assert.Single(result.Value.Items);
            Assert.Equal(LatteId, item.Id);
            Assert.Equal("Latte", item.Name);
            Assert.Equal(20m, item.PriceFrom);
            Assert.Equal("Milky", item.Reason);
            Assert.Equal(Code, Assert.IsType<ResolveGuestQrQuery>(sender.LastRequest).Code);
            Assert.Equal(BranchId, Assert.Single(store.ListedBranchIds));
            Assert.Equal("something milky", Assert.IsType<ProductDiscoveryInput>(provider.LastInput).Text);
        }

        [Fact]
        public async Task Handle_AnUnusableQrReturnsTheUniformNotFoundWithoutCallingTheProvider()
        {
            var store = new FakeGuestMenuStore();
            var provider = new FakeProvider();
            var sender = new RecordingSender
            {
                Response = Result.Failure<GuestQrContext>(GuestQrErrors.NotFound()),
            };
            var handler = new GetProductDiscoveryQueryHandler(sender, Generator(provider, store));

            var result = await handler.Handle(
                new GetProductDiscoveryQuery { Code = Code, Query = "cold" },
                CancellationToken.None);

            Assert.False(result.IsSuccess);
            Assert.Equal(GuestQrErrors.NotFoundCode, result.Error!.Code);
            Assert.Null(provider.LastInput);
            Assert.Empty(store.ListedBranchIds);
        }

        private static ProductDiscoveryGenerator Generator(
            FakeProvider provider,
            FakeGuestMenuStore store) =>
            new(provider, store, NullLogger<ProductDiscoveryGenerator>.Instance);

        private static GuestQrContext Context() =>
            new("Cafe", BranchId, "Downtown", Guid.NewGuid(), 5);

        private sealed class FakeProvider : IProductDiscoveryProvider
        {
            public Result<IReadOnlyList<ProductDiscoverySuggestion>> Response { get; init; } =
                Result.Success<IReadOnlyList<ProductDiscoverySuggestion>>(
                    Array.Empty<ProductDiscoverySuggestion>());

            public ProductDiscoveryInput? LastInput { get; private set; }

            public Task<Result<IReadOnlyList<ProductDiscoverySuggestion>>> SuggestAsync(
                ProductDiscoveryInput input,
                CancellationToken cancellationToken)
            {
                LastInput = input;
                return Task.FromResult(Response);
            }
        }

        private sealed class FakeGuestMenuStore : IGuestMenuStore
        {
            public IReadOnlyList<GuestMenuProductSummary> Visible { get; init; } = [];

            public List<Guid> ListedBranchIds { get; } = [];

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
                CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<GuestMenuProductSummary>>([]);
        }

        private sealed class RecordingSender : ISender
        {
            public object? Response { get; init; }

            public object? LastRequest { get; private set; }

            public Task<TResponse> Send<TResponse>(
                IRequest<TResponse> request,
                CancellationToken cancellationToken = default)
            {
                LastRequest = request;
                return Task.FromResult((TResponse)Response!);
            }

            public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
                where TRequest : IRequest =>
                throw new NotSupportedException("The handler must send a request with a response.");

            public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The handler must send a typed request.");

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The handler does not create streams.");

            public IAsyncEnumerable<object?> CreateStream(
                object request,
                CancellationToken cancellationToken = default) =>
                throw new NotSupportedException("The handler does not create streams.");
        }
    }
}
