using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Tenancy;

namespace CafeHub.Application.Stations.Commands
{
    public sealed class SetStaffStationsCommandHandler : ICommandHandler<SetStaffStationsCommand>
    {
        private readonly IStationStore stationStore;
        private readonly ICurrentTenantUser currentTenantUser;
        private readonly ITenantContextAccessor tenantContextAccessor;

        public SetStaffStationsCommandHandler(
            IStationStore stationStore,
            ICurrentTenantUser currentTenantUser,
            ITenantContextAccessor tenantContextAccessor)
        {
            this.stationStore = stationStore ?? throw new ArgumentNullException(nameof(stationStore));
            this.currentTenantUser = currentTenantUser ?? throw new ArgumentNullException(nameof(currentTenantUser));
            this.tenantContextAccessor = tenantContextAccessor ?? throw new ArgumentNullException(nameof(tenantContextAccessor));
        }

        public async Task<Result> Handle(SetStaffStationsCommand command, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);

            var tenantContext = tenantContextAccessor.GetRequired();
            if (!StationActor.TryGetActorUserId(currentTenantUser, tenantContext, out var userId))
            {
                return Result.Failure(AuthErrors.CurrentTenantUserUnavailable());
            }

            var scope = TenantBranchScope.Resolve(currentTenantUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure(scope.Error!);
            }

            return await stationStore.SetStaffStationsAsync(
                command.StaffId,
                command.StationIds,
                scope.Value,
                userId,
                cancellationToken);
        }
    }
}
