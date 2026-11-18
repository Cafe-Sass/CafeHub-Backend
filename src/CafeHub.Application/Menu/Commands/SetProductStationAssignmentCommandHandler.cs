using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class SetProductStationAssignmentCommandHandler(
        IProductStationAssignmentStore store, ICurrentTenantUser currentUser, ITenantContextAccessor tenantContext)
        : ICommandHandler<SetProductStationAssignmentCommand, ProductStationAssignmentResponse>
    {
        public Task<Result<ProductStationAssignmentResponse>> Handle(
            SetProductStationAssignmentCommand request, CancellationToken cancellationToken)
        {
            var error = ProductActor.CheckBranch(currentUser, tenantContext, request.BranchId, out var actorId);
            return error is null
                ? store.SetAsync(request.ProductId, request.BranchId, request.StationId,
                    request.ConcurrencyToken, actorId, cancellationToken)
                : Task.FromResult(Result.Failure<ProductStationAssignmentResponse>(error));
        }
    }
}
