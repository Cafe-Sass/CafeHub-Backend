using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Tables.DTOs;

namespace CafeHub.Application.Tables.Commands
{
    /// <summary>
    /// Adds N tables (1 to 200) to an active branch, numbered consecutively after the highest active
    /// number. Marked as a tenant transactional command so the inserts and the single tenant audit entry
    /// commit together in one tenant transaction.
    /// </summary>
    public sealed record AddTablesBulkCommand(Guid BranchId, int Count)
        : ICommand<TableBulkResult>, ITenantTransactionalCommand;
}
