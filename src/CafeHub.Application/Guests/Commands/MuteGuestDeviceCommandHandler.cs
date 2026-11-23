using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Guests.DTOs;
using CafeHub.Application.Identity;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Guests.Commands
{
    public sealed class MuteGuestDeviceCommandHandler(
        IGuestMuteStore store,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<MuteGuestDeviceCommand, GuestMuteResult>
    {
        public async Task<Result<GuestMuteResult>> Handle(
            MuteGuestDeviceCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<GuestMuteResult>(scope.Error!);
            }

            if (currentUser.UserId is not { } actorId)
            {
                return Result.Failure<GuestMuteResult>(AuthErrors.Forbidden());
            }

            var target = await store.FindOrderAsync(scope.Value, command.OrderId, cancellationToken);
            if (target is null)
            {
                return Result.Failure<GuestMuteResult>(GuestMuteErrors.NotFound());
            }

            if (!Order.IsValidGuestDeviceId(target.DeviceId))
            {
                return Result.Failure<GuestMuteResult>(GuestMuteErrors.DeviceRequired());
            }

            var now = timeProvider.GetUtcNow();
            var existing = await store.FindActiveAsync(target.BranchId, target.DeviceId!, now, cancellationToken);
            if (existing is not null)
            {
                return Result.Success(GuestMuteResult.From(existing));
            }

            var mute = GuestMute.Create(
                Guid.CreateVersion7(),
                target.BranchId,
                target.OrderId,
                target.DeviceId!,
                actorId,
                command.Reason,
                now,
                command.ExpiryHours,
                actorId);

            var created = await store.CreateAsync(mute, cancellationToken);
            if (created.IsFailure)
            {
                return Result.Failure<GuestMuteResult>(created.Error!);
            }

            return Result.Success(new GuestMuteResult(
                mute.Id,
                mute.OrderId,
                target.OrderNumber,
                mute.Reason,
                mute.CreatedAt,
                mute.ExpiresAt,
                null,
                mute.MutedByUserId));
        }
    }
}
