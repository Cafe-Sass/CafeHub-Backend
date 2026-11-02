using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Commands
{
    /// <summary>
    /// Retires a tenant branch. Retired is terminal. Marked as a tenant transactional command so the status
    /// change and the tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record RetireBranchCommand(Guid BranchId, string ConcurrencyToken)
        : ICommand<BranchResponse>, ITenantTransactionalCommand;
}
