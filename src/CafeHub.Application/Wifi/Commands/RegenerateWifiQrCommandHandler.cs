using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Commands
{
    public sealed class RegenerateWifiQrCommandHandler(
        IWifiQrStore store,
        ICurrentTenantUser currentUser)
        : ICommandHandler<RegenerateWifiQrCommand, WifiQrData>
    {
        public async Task<Result<WifiQrData>> Handle(
            RegenerateWifiQrCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<WifiQrData>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != command.BranchId)
            {
                return Result.Failure<WifiQrData>(WifiErrors.NotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiQrData>(AuthErrors.Forbidden());
            }

            return await store.RegenerateAsync(scope.Value, command.BranchId, actorId, cancellationToken);
        }
    }
}
