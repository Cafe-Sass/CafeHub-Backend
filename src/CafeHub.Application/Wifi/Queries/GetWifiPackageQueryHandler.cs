using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetWifiPackageQueryHandler(IWifiPackageStore store)
        : IQueryHandler<GetWifiPackageQuery, WifiPackageResponse>
    {
        public async Task<Result<WifiPackageResponse>> Handle(
            GetWifiPackageQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var package = await store.GetAsync(query.PackageId, cancellationToken);
            return package is null
                ? Result.Failure<WifiPackageResponse>(WifiErrors.PackageNotFound())
                : Result.Success(package);
        }
    }
}
