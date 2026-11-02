using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Commands
{
    /// <summary>
    /// Suspends a tenant branch. Marked as a tenant transactional command so the status change and the
    /// tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record SuspendBranchCommand(Guid BranchId, string ConcurrencyToken)
        : ICommand<BranchResponse>, ITenantTransactionalCommand;
}
