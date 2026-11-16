using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Contracts
{
    public interface IProductStore
    {
        Task<Result<ProductResponse>> CreateAsync(
            ProductWriteModel product, Guid actorId, CancellationToken cancellationToken);

        Task<Result<ProductResponse>> UpdateAsync(
            Guid id, ProductWriteModel product, uint version, Guid actorId, CancellationToken cancellationToken);

        Task<Result<ProductResponse>> ChangeStatusAsync(
            Guid id, ProductStatus status, uint version, Guid actorId, CancellationToken cancellationToken);
    }
}
