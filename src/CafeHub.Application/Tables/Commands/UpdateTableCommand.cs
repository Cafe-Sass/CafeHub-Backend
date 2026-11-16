using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    /// <summary>
    /// Updates the label of a table in the caller's branch scope. Marked as a tenant transactional
    /// command so the update and the tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record UpdateTableCommand(Guid TableId, string? Label, string ConcurrencyToken)
        : ICommand<TableResponse>, ITenantTransactionalCommand;
}
