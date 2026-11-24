using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetWifiSettingsQueryHandler(
        IWifiSettingsStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetWifiSettingsQuery, WifiSettingsResponse>
    {
        public async Task<Result<WifiSettingsResponse>> Handle(
            GetWifiSettingsQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<WifiSettingsResponse>(scope.Error!);
            }

            return Result.Success(await store.GetAsync(scope.Value, cancellationToken));
        }
    }
}
