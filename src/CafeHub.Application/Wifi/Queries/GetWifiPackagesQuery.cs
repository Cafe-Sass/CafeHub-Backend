using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed record GetWifiPackagesQuery : IQuery<Pagination.OffsetPage<WifiPackageListItem>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public WifiPackageStatus? Status { get; init; }
    }
}
