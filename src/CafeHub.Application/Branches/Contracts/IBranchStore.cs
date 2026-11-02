using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Branches.DTOs;

namespace CafeHub.Application.Branches.Contracts
{
    /// <summary>
    /// Persistence port for a tenant's branches. The tenant is resolved from the write-once tenant
    /// context; the store never accepts a tenant identifier from callers. Create and update commit the
    /// branch change and one tenant audit entry together in a single tenant transaction.
    /// </summary>
    public interface IBranchStore
    {
        Task<Result<BranchResponse>> CreateAsync(
            string name,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<Result<BranchResponse>> UpdateAsync(
            Guid branchId,
            string name,
            string? address,
            string? contactPhone,
            string? openingHoursSummary,
            string concurrencyToken,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
