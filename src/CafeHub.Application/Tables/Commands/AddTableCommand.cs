using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    /// <summary>
    /// Adds one table to an active branch, numbered after the highest active number (or 1 when none).
    /// Marked as a tenant transactional command so the insert and the tenant audit entry commit together
    /// in one tenant transaction.
    /// </summary>
    public sealed record AddTableCommand(Guid BranchId, string? Label)
        : ICommand<TableResponse>, ITenantTransactionalCommand;
}
