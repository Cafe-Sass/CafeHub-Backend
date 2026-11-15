using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Stations.DTOs;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Stations.Commands
{
    public sealed class ReactivateStationCommandHandler : ICommandHandler<ReactivateStationCommand, StationResponse>
    {
        private readonly IStationStore stationStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public ReactivateStationCommandHandler(
            IStationStore stationStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.stationStore = stationStore ?? throw new ArgumentNullException(nameof(stationStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result<StationResponse>> Handle(ReactivateStationCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var tenantContext = tenantContextAccessor.GetRequired();
            if (!StationActor.TryGetActorUserId(currentTenantUser, tenantContext, out var userId))
            {
                return Result.Failure<StationResponse>(AuthErrors.CurrentTenantUserUnavailable());
            }

            var scope = TenantBranchScope.Resolve(currentTenantUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<StationResponse>(scope.Error!);
            }

            return await stationStore.ReactivateAsync(
                command.StationId,
                scope.Value,
                command.ConcurrencyToken,
                userId,
                cancellationToken);
        }
    }
}
