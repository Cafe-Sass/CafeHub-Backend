using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ResetStaffPasswordCommandHandler : ICommandHandler<ResetStaffPasswordCommand>
    {
        private readonly IStaffStore staffStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public ResetStaffPasswordCommandHandler(
            IStaffStore staffStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.staffStore = staffStore ?? throw new ArgumentNullException(nameof(staffStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result> Handle(ResetStaffPasswordCommand command, CancellationToken cancellationToken)
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
                return Result.Failure(AuthErrors.CurrentTenantUserUnavailable());
            }

            return await staffStore.ResetPasswordAsync(
                command.StaffId,
                command.NewPassword,
                userId.Value,
                cancellationToken);
        }
    }
}
