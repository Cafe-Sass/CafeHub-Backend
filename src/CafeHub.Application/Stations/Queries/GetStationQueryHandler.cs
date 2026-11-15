using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Stations.Contracts;
using CafeHub.Application.Stations.DTOs;

namespace CafeHub.Application.Stations.Queries
{
    public sealed class GetStationQueryHandler : IQueryHandler<GetStationQuery, StationResponse>
    {
        private readonly IStationStore stationStore;
        private readonly ICurrentTenantUser currentUser;

        public GetStationQueryHandler(IStationStore stationStore, ICurrentTenantUser currentUser)
        {
            this.stationStore = stationStore ?? throw new ArgumentNullException(nameof(stationStore));
            this.currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        }

        public async Task<Result<StationResponse>> Handle(GetStationQuery query, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<StationResponse>(scope.Error!);
            }

            var station = await stationStore.GetByIdAsync(query.StationId, scope.Value, cancellationToken);
            return station is null
                ? Result.Failure<StationResponse>(StationsErrors.NotFound())
                : Result.Success(station);
        }
    }
}
