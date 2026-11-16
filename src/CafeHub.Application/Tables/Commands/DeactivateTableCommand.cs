using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    /// <summary>
    /// Deactivates a table in the caller's branch scope. The row is kept; only the status changes.
    /// Marked as a tenant transactional command so the update and the tenant audit entry commit together.
    /// </summary>
    public sealed record DeactivateTableCommand(Guid TableId, string ConcurrencyToken)
        : ICommand<TableResponse>, ITenantTransactionalCommand;
}
