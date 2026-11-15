using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Staff.Contracts;
using CafeHub.Application.Staff.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Staff.Commands
{
    public sealed class CreateStaffCommandHandler : ICommandHandler<CreateStaffCommand, StaffResponse>
    {
        private readonly IStaffStore staffStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public CreateStaffCommandHandler(
            IStaffStore staffStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.staffStore = staffStore ?? throw new ArgumentNullException(nameof(staffStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<StaffResponse>> Handle(CreateStaffCommand command, CancellationToken cancellationToken)
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
                return Result.Failure<StaffResponse>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var fullName = command.FullName.Trim();
            var email = command.Email.Trim();
            var phoneNumber = NormalizeOptional(command.PhoneNumber);

            return await staffStore.CreateAsync(
                fullName,
                email,
                phoneNumber,
                command.BranchId,
                command.Password,
                userId.Value,
                cancellationToken);
        }

        private static string? NormalizeOptional(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }
    }
}
