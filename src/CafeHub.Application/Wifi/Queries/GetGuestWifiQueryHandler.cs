using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Tables;
using CafeHub.Application.Tenancy;
using CafeHub.Application.Wifi.Contracts;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed class GetGuestWifiQueryHandler(
        IWifiQrStore store,
        ITenantContextAccessor tenantContextAccessor)
        : IQueryHandler<GetGuestWifiQuery, GuestWifiInfo>
    {
        public async Task<Result<GuestWifiInfo>> Handle(
            GetGuestWifiQuery query,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (!BranchWifiQr.IsValidCode(query.Code) || !tenantContextAccessor.GetRequired().IsActive)
            {
                return Result.Failure<GuestWifiInfo>(GuestQrErrors.NotFound());
            }

            var context = await store.ResolveAsync(query.Code!, cancellationToken);
            if (context is null)
            {
                return Result.Failure<GuestWifiInfo>(GuestQrErrors.NotFound());
            }

            if (context.Policy == WifiPolicy.Free)
            {
                if (context.Network is null)
                {
                    return Result.Failure<GuestWifiInfo>(GuestQrErrors.NotFound());
                }

                return Result.Success(new GuestWifiInfo(
                    WifiPolicy.Free.ToString(),
                    new GuestWifiNetwork(
                        context.Network.Ssid,
                        context.Network.Security.ToString(),
                        context.Network.Password),
                    []));
            }

            var packages = context.Packages
                .Where(package => package.Status == WifiPackageStatus.Active)
                .OrderBy(package => package.SortOrder)
                .ThenBy(package => package.Id)
                .Select(package => new GuestWifiPackage(
                    package.Name,
                    package.DurationMinutes,
                    package.SpeedMbps,
                    package.DataMode.ToString(),
                    package.QuotaMb,
                    package.Price))
                .ToArray();

            return Result.Success(new GuestWifiInfo(
                WifiPolicy.Paid.ToString(),
                null,
                packages));
        }
    }
}
