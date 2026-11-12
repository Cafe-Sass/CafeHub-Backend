using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Queries
{
    public sealed record GetStaffByIdQuery(Guid StaffId) : IQuery<StaffResponse>;
}
