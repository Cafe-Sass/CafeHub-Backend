using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Branches.Commands
{
    public sealed class SuspendBranchCommandHandler : ICommandHandler<SuspendBranchCommand, BranchResponse>
    {
        private const string SucceededAction = "branches.suspended.succeeded";

        private readonly IBranchStore branchStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public SuspendBranchCommandHandler(
            IBranchStore branchStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.branchStore = branchStore ?? throw new ArgumentNullException(nameof(branchStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<BranchResponse>> Handle(SuspendBranchCommand command, CancellationToken cancellationToken)
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
            if (currentStatus == BranchLifecycleStatus.Suspended)
            {
                return Result.Success(branch);
            }

            if (!BranchLifecycleTransitions.IsLegalTransition(currentStatus, BranchLifecycleStatus.Suspended))
            {
                return Result.Failure<BranchResponse>(BranchesErrors.IllegalTransition());
            }

            return await branchStore.ChangeLifecycleStatusAsync(
                command.BranchId,
                BranchLifecycleStatus.Suspended,
                command.ConcurrencyToken,
                userId.Value,
                SucceededAction,
                cancellationToken);
        }
    }
}
