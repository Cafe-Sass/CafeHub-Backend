using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Staff.DTOs;

namespace CafeHub.Application.Staff.Commands
{
    public sealed record CreateStaffCommand(
        string FullName,
        string Email,
        string? PhoneNumber,
        Guid BranchId,
        string Password) : ICommand<StaffResponse>, ITenantTransactionalCommand;
}
