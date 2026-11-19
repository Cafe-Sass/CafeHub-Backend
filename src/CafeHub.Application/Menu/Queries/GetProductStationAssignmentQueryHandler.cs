using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Queries
{
    public sealed class GetProductStationAssignmentQueryHandler(
        IProductStationAssignmentStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : IQueryHandler<GetProductStationAssignmentQuery, ProductStationAssignmentResponse>
    {
        public Task<Result<ProductStationAssignmentResponse>> Handle(
            GetProductStationAssignmentQuery request, CancellationToken cancellationToken)
        {
            var error = ProductActor.CheckBranch(currentUser, tenantContext, request.BranchId, out _);
            return error is null
                ? store.GetAsync(request.ProductId, request.BranchId, cancellationToken)
                : Task.FromResult(Result.Failure<ProductStationAssignmentResponse>(error));
        }
    }
}
