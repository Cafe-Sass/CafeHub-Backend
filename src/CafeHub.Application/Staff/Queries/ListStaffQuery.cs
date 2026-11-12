using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Pagination;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Queries
{
    public sealed record ListStaffQuery : IQuery<OffsetPage<StaffResponse>>
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }

        public Guid? BranchId { get; init; }

        public bool? IsActive { get; init; }
    }
}
