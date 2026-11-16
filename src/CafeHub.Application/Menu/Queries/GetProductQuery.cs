using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Menu.Queries
{
    public sealed record GetProductQuery(Guid Id) : IQuery<ProductResponse>;
}
