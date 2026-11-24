using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Identity;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetWifiQrQueryHandler(
        IWifiQrStore store,
        ICurrentTenantUser currentUser)
        : IQueryHandler<GetWifiQrQuery, WifiQrData>
    {
        public async Task<Result<WifiQrData>> Handle(
            GetWifiQrQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var scope = TenantBranchScope.Resolve(currentUser.AccountAccess);
            if (scope.IsFailure)
            {
                return Result.Failure<WifiQrData>(scope.Error!);
            }

            if (scope.Value.HasValue && scope.Value != query.BranchId)
            {
                return Result.Failure<WifiQrData>(WifiErrors.NotFound());
            }

            if (currentUser.UserId is not { } actorId || actorId == Guid.Empty)
            {
                return Result.Failure<WifiQrData>(AuthErrors.Forbidden());
            }

            var qr = await store.GetOrCreateAsync(scope.Value, query.BranchId, actorId, cancellationToken);
            return qr;
        }
    }
}
