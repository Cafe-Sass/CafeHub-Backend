using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Commands
{
    /// <summary>
    /// Creates a tenant branch. Marked as a tenant transactional command so the branch insert and the
    /// tenant audit entry commit together in one tenant transaction. A new branch starts Active.
    /// </summary>
    public sealed record CreateBranchCommand(
        string Name,
        string? Address,
        string? ContactPhone,
        string? OpeningHoursSummary) : ICommand<BranchResponse>, ITenantTransactionalCommand;
}
