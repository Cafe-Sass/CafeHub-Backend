using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Guests.Contracts;
using CafeHub.Application.Guests.DTOs;
using CafeHub.Application.Identity;

namespace CafeHub.Application.Guests.Commands
{
    public sealed class UnmuteGuestDeviceCommandHandler(
        IGuestMuteStore store,
        ICurrentTenantUser currentUser,
        TimeProvider timeProvider)
        : ICommandHandler<UnmuteGuestDeviceCommand, GuestMuteResult>
    {
        public async Task<Result<GuestMuteResult>> Handle(
            UnmuteGuestDeviceCommand command,
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

            var entry = await store.FindAsync(scope.Value, command.MuteId, cancellationToken);
            if (entry is null)
            {
                return Result.Failure<GuestMuteResult>(GuestMuteErrors.NotFound());
            }

            var now = timeProvider.GetUtcNow();
            if (entry.LiftedAt is not null || entry.ExpiresAt <= now)
            {
                return Result.Success(GuestMuteResult.From(entry));
            }

            var lifted = await store.LiftAsync(entry.Id, actorId, now, cancellationToken);
            if (lifted.IsFailure)
            {
                return Result.Failure<GuestMuteResult>(lifted.Error!);
            }

            var liftedByName = await store.FindUserNameAsync(actorId, cancellationToken);
            return Result.Success(GuestMuteResult.From(entry) with
            {
                LiftedAt = now,
                LiftedByName = liftedByName,
            });
        }
    }
}
