namespace CafeHub.Application.Wifi.Contracts
{
    public sealed record IssuedWifiListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid? BranchId,
        Guid? PackageId,
        IssuedWifiStatus? Status,
        int? OrderNumber,
        DateOnly? Date);
}
