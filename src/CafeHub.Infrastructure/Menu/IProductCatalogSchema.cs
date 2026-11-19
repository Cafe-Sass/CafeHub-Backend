using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Infrastructure.Menu
{
    internal interface IProductCatalogSchema
    {
        Task<Result<T>> RunAsync<T>(
            Func<Task<Result<T>>> operation, CancellationToken cancellationToken, bool repeatableRead = false);
    }
}
