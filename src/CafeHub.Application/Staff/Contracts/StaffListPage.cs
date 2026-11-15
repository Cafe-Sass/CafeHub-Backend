using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Contracts
{
    public sealed record StaffListPage(IReadOnlyList<StaffResponse> FetchedItems, int? TotalCount);
}
