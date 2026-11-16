using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    /// <summary>
    /// Reactivates a table in the caller's branch scope. Fails with Conflict when an active table already
    /// uses the number. Marked as a tenant transactional command so the update and the tenant audit entry
    /// commit together.
    /// </summary>
    public sealed record ReactivateTableCommand(Guid TableId, string ConcurrencyToken)
        : ICommand<TableResponse>, ITenantTransactionalCommand;
}
