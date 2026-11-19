using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Pagination;
using CafeHub.Domain.Tenants;

namespace CafeHub.UnitTests.Menu.Commands
{
    internal sealed class FakeProductStore : IProductStore
    {
        public List<ProductCreateCall> Created { get; } = new();
        public List<ProductUpdateCall> Updated { get; } = new();
        public List<ProductStatusCall> StatusChanges { get; } = new();
        public ProductResponse? Response { get; set; }
        public Error? Failure { get; set; }

        public Task<Result<ProductResponse>> CreateAsync(
            ProductWriteModel product, Guid actorId, CancellationToken cancellationToken)
        {
            Created.Add(new ProductCreateCall(product, actorId));
            return Reply();
        }

        public Task<Result<ProductResponse>> UpdateAsync(
            Guid id, ProductWriteModel product, uint version, Guid actorId, CancellationToken cancellationToken)
        {
            Updated.Add(new ProductUpdateCall(id, product, version, actorId));
            return Reply();
        }

        public Task<Result<ProductResponse>> ChangeStatusAsync(
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken,
            bool includeHiddenCategories = false)
        {
            StatusChanges.Add(new ProductStatusCall(id, status, version, actorId, includeHiddenCategories));
            return Reply();
        }

        public List<ListProductsCall> Listed { get; } = new();
        public List<Guid> Gotten { get; } = new();

        public Task<Result<OffsetPage<ProductListItemResponse>>> ListAsync(
            Guid? categoryId,
            ProductKind? kind,
            bool? caffeine,
            string? tag,
            ProductStatus? status,
            string? search,
            int page,
            int pageSize,
            IReadOnlyList<SortTerm> sort,
            bool includeTotalCount,
            CancellationToken cancellationToken,
            bool includeHiddenCategories = false)
        {
            Listed.Add(new ListProductsCall(categoryId, kind, caffeine, tag, status, search, page, pageSize, sort, includeTotalCount));
            return Task.FromResult(Failure is null
                ? Result.Success(OffsetPage<ProductListItemResponse>.Create([], page, pageSize, null))
                : Result.Failure<OffsetPage<ProductListItemResponse>>(Failure));
        }

        public Task<Result<ProductResponse>> GetAsync(Guid id, CancellationToken cancellationToken, bool includeHiddenCategories = false)
        {
            Gotten.Add(id);
            return Task.FromResult(Failure is { } error
                ? Result.Failure<ProductResponse>(error)
                : Response is { } product
                    ? Result.Success(product)
                    : Result.Failure<ProductResponse>(ProductErrors.NotFound()));
        }

        public static ProductResponse Product(Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Name = "Latte",
            Description = null,
            Ingredients = null,
            Kind = nameof(ProductKind.Drink),
            Caffeine = true,
            PreparationMinutes = null,
            SortOrder = 0,
            Status = nameof(ProductStatus.Active),
            Sizes = [],
            Addons = [],
            Tags = [],
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
            ConcurrencyToken = "1",
        };

        private Task<Result<ProductResponse>> Reply() => Task.FromResult(Failure is null
            ? Result.Success(Response!)
            : Result.Failure<ProductResponse>(Failure));
    }

    internal sealed record ProductCreateCall(ProductWriteModel Product, Guid ActorId);

    internal sealed record ProductUpdateCall(Guid Id, ProductWriteModel Product, uint Version, Guid ActorId);

    internal sealed record ProductStatusCall(Guid Id, ProductStatus Status, uint Version, Guid ActorId, bool IncludeHiddenCategories);

    internal sealed record ListProductsCall(
        Guid? CategoryId,
        ProductKind? Kind,
        bool? Caffeine,
        string? Tag,
        ProductStatus? Status,
        string? Search,
        int Page,
        int PageSize,
        IReadOnlyList<SortTerm> Sort,
        bool IncludeTotalCount);
}
