using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.CafeProfile.Contracts;
using CafeHub.Application.CafeProfile.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.CafeProfile.Commands
{
    public sealed class UpdateCafeProfileCommandHandler : ICommandHandler<UpdateCafeProfileCommand, CafeProfileResponse>
    {
        private readonly ICafeSettingsWriter writer;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;
        private readonly TimeProvider timeProvider;

        public UpdateCafeProfileCommandHandler(
            ICafeSettingsWriter writer,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor,
            TimeProvider timeProvider)
        {
            this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public async Task<Result<CafeProfileResponse>> Handle(UpdateCafeProfileCommand command, CancellationToken cancellationToken)
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
                return Result.Failure<CafeProfileResponse>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var cafeName = command.CafeName.Trim();
            var logoMediaReference = NormalizeOptional(command.LogoMediaReference);
            var primaryColor = NormalizeOptional(command.PrimaryColor);
            var secondaryColor = NormalizeOptional(command.SecondaryColor);
            var accentColor = NormalizeOptional(command.AccentColor);
            var contactPhone = NormalizeOptional(command.ContactPhone);
            var contactEmail = NormalizeOptional(command.ContactEmail);
            var contactAddress = NormalizeOptional(command.ContactAddress);
            var timezone = command.Timezone.Trim();

            var updatedAt = timeProvider.GetUtcNow();

            return await writer.UpdateAsync(
                cafeName,
                logoMediaReference,
                primaryColor,
                secondaryColor,
                accentColor,
                contactPhone,
                contactEmail,
                contactAddress,
                timezone,
                userId.Value,
                command.ConcurrencyToken,
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
