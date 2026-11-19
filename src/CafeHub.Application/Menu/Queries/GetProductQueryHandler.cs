using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetProductQueryHandler(IProductStore store) : IQueryHandler<GetProductQuery, ProductResponse>
    {
        public Task<Result<ProductResponse>> Handle(GetProductQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            return store.GetAsync(query.Id, cancellationToken, query.IncludeHiddenCategories);
        }
    }
}
