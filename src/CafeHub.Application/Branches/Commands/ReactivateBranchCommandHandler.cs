using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches.Commands
{
    public sealed class ReactivateBranchCommandHandler : ICommandHandler<ReactivateBranchCommand, BranchResponse>
    {
        private const string SucceededAction = "branches.reactivated.succeeded";

        private readonly IBranchStore branchStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public ReactivateBranchCommandHandler(
            IBranchStore branchStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.branchStore = branchStore ?? throw new ArgumentNullException(nameof(branchStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<BranchResponse>> Handle(ReactivateBranchCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var tenantContext = tenantContextAccessor.GetRequired();
            var userId = currentTenantUser.UserId;
            if (!currentTenantUser.IsAuthenticated
                || userId is null
                || userId == Guid.Empty
                || currentTenantUser.TenantId is null
                || currentTenantUser.TenantId != tenantContext.TenantId
                || !tenantContext.IsActive)
            {
                return Result.Failure<BranchResponse>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var branch = await branchStore.GetByIdAsync(command.BranchId, cancellationToken);
            if (branch is null)
            {
                return Result.Failure<BranchResponse>(BranchesErrors.NotFound());
            }

            var currentStatus = Enum.Parse<BranchLifecycleStatus>(branch.LifecycleStatus);
            if (currentStatus == BranchLifecycleStatus.Active)
            {
                return Result.Success(branch);
            }

            if (!BranchLifecycleTransitions.IsLegalTransition(currentStatus, BranchLifecycleStatus.Active))
            {
                return Result.Failure<BranchResponse>(BranchesErrors.IllegalTransition());
            }

            return await branchStore.ChangeLifecycleStatusAsync(
                command.BranchId,
                BranchLifecycleStatus.Active,
                command.ConcurrencyToken,
                userId.Value,
                SucceededAction,
                cancellationToken);
        }
    }
}
