using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetWifiVoucherSummaryQueryHandler(
        IWifiVoucherStore vouchers,
        IWifiPackageStore packages)
        : IQueryHandler<GetWifiVoucherSummaryQuery, WifiVoucherSummary>
    {
        public async Task<Result<WifiVoucherSummary>> Handle(
            GetWifiVoucherSummaryQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            var package = await packages.GetAsync(query.PackageId, cancellationToken);
            if (package is null)
            {
                return Result.Failure<WifiVoucherSummary>(WifiErrors.PackageNotFound());
            }

            return Result.Success(await vouchers.GetSummaryAsync(query.PackageId, cancellationToken));
        }
    }
}
