using CafeHub.Application.Abstractions.Messaging;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Commands
{
    /// <summary>
    /// Updates a tenant branch's name, address, contact phone and opening-hours summary only. The
    /// lifecycle status is never changed by this command. Marked as a tenant transactional command so
    /// the update and the tenant audit entry commit together in one tenant transaction.
    /// </summary>
    public sealed record UpdateBranchCommand(
        Guid BranchId,
        string Name,
        string? Address,
        string? ContactPhone,
        string? OpeningHoursSummary,
        string ConcurrencyToken) : ICommand<BranchResponse>, ITenantTransactionalCommand;
}
