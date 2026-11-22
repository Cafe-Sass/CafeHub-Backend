using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Menu.Contracts;
using CafeHub.Application.Menu.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Menu.Commands
{
    public sealed class SetBranchProductAvailabilityCommandHandler(
        IBranchProductAvailabilityStore store,
        ICurrentTenantUser currentUser,
        ITenantContextAccessor tenantContext)
        : ICommandHandler<SetBranchProductAvailabilityCommand, BranchProductAvailabilityResponse>
    {
        public Task<Result<BranchProductAvailabilityResponse>> Handle(
            SetBranchProductAvailabilityCommand request, CancellationToken cancellationToken)
        {
            var error = ProductActor.CheckBranch(currentUser, tenantContext, request.BranchId, out var actorId);
            return error is null
                ? store.SetAsync(
                    request.BranchId, request.ProductId, request.Available, request.Reason, actorId, cancellationToken)
                : Task.FromResult(Result.Failure<BranchProductAvailabilityResponse>(error));
        }
    }
}
