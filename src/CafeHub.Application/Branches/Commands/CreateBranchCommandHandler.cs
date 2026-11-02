using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches.Contracts;
using CafeHub.Application.Branches.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Branches.Commands
{
    public sealed class CreateBranchCommandHandler : ICommandHandler<CreateBranchCommand, BranchResponse>
    {
        private readonly IBranchStore branchStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public CreateBranchCommandHandler(
            IBranchStore branchStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.branchStore = branchStore ?? throw new ArgumentNullException(nameof(branchStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<BranchResponse>> Handle(CreateBranchCommand command, CancellationToken cancellationToken)
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

            var name = command.Name.Trim();
            var address = NormalizeOptional(command.Address);
            var contactPhone = NormalizeOptional(command.ContactPhone);
            var openingHoursSummary = NormalizeOptional(command.OpeningHoursSummary);

            return await branchStore.CreateAsync(
                name,
                address,
                contactPhone,
                openingHoursSummary,
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
