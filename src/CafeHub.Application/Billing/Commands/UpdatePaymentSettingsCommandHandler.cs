using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Billing.Contracts;
using CafeHub.Application.Billing.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Billing.Commands
{
    public sealed class UpdatePaymentSettingsCommandHandler(
        ICafePaymentSettingsWriter writer,
        ICurrentTenantUser currentUser)
        : ICommandHandler<UpdatePaymentSettingsCommand, PaymentSettingsResponse>
    {
        public async Task<Result<PaymentSettingsResponse>> Handle(
            UpdatePaymentSettingsCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<PaymentSettingsResponse>(AuthErrors.Forbidden());
            }

            return await writer.UpdateAsync(
                command.OnlineEnabled,
                command.MethodLabel,
                command.PaymentLink,
                command.PaymentAddress,
                command.DisplayName,
                actorId,
                command.ConcurrencyToken,
                cancellationToken);
        }
    }
}
