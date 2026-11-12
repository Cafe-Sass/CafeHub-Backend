namespace CafeHub.Application.Staff.Contracts
{
    public sealed record StaffListRequest(
        int Page,
        int PageSize,
        bool IncludeTotalCount,
        Guid? BranchId,
        bool? IsActive);
}
