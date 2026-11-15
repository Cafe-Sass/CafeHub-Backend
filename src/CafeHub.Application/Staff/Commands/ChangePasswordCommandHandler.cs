using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
    {
        private readonly IStaffStore staffStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public ChangePasswordCommandHandler(
            IStaffStore staffStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.staffStore = staffStore ?? throw new ArgumentNullException(nameof(staffStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
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

            return await staffStore.ChangePasswordAsync(
                userId.Value,
                command.CurrentPassword,
                command.NewPassword,
                cancellationToken);
        }
    }
}
