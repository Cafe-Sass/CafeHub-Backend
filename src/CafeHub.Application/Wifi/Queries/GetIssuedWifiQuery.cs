using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Wifi.DTOs;

namespace CafeHub.Application.Wifi.Queries
{
    public sealed record GetIssuedWifiQuery : IQuery<OffsetPage<IssuedWifiItem>>, ISensitiveRequest
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public Guid? PackageId { get; init; }

        public IssuedWifiStatus? Status { get; init; }

        public int? OrderNumber { get; init; }

        public DateOnly? Date { get; init; }
    }
}
