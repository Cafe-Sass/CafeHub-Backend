using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Commands
{
    public sealed record UpdateStaffCommand(
        Guid StaffId,
        string FullName,
        string? PhoneNumber,
        Guid BranchId,
        string ConcurrencyToken) : ICommand<StaffResponse>, ITenantTransactionalCommand;
}
