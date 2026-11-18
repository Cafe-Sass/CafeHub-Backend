using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Menu.DTOs;

namespace CafeHub.Application.Menu.Queries
{
    public sealed record GetProductStationAssignmentQuery(Guid ProductId, Guid BranchId)
        : IQuery<ProductStationAssignmentResponse>;
}
