using CafeHub.Application.Abstractions.Results;
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
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken)
        {
            StatusChanges.Add(new ProductStatusCall(id, status, version, actorId));
            return Reply();
        }

        public List<ListProductsCall> Listed { get; } = new();
        public List<Guid> Gotten { get; } = new();

        public Task<OffsetPage<ProductListItemResponse>> ListAsync(
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
            CancellationToken cancellationToken)
        {
            Listed.Add(new ListProductsCall(categoryId, kind, caffeine, tag, status, search, page, pageSize, sort, includeTotalCount));
            return Task.FromResult(OffsetPage<ProductListItemResponse>.Create([], page, pageSize, null));
        }

        public Task<ProductResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Gotten.Add(id);
            return Task.FromResult(Response);
        }

        public static ProductResponse Product(Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            StationId = null,
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

    internal sealed record ProductStatusCall(Guid Id, ProductStatus Status, uint Version, Guid ActorId);

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
