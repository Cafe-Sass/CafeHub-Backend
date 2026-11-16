using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    /// <summary>
    /// Deactivates the highest N active tables of an active branch. Marked as a tenant transactional
    /// command so the deactivations and the single tenant audit entry commit together in one tenant
    /// transaction.
    /// </summary>
    public sealed record DeactivateHighestTablesCommand(Guid BranchId, int Count)
        : ICommand<TableBulkResult>, ITenantTransactionalCommand;
}
