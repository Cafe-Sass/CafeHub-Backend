using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Printing.Contracts
{
    public sealed record PrintConnectorAccess(
        Guid Id,
        Guid BranchId,
        string Name,
        string KeyHash,
        PrintConnectorStatus Status);

    /// <summary>
    /// Persistence port for per-branch connector registrations. Only the key hash is stored; creation and
    /// revocation commit their change and one tenant audit entry together, and the hash never enters an
    /// audit snapshot.
    /// </summary>
    public interface IPrintConnectorStore
    {
        Task<PrintConnectorAccess?> FindByKeyHashAsync(
            string keyHash,
            CancellationToken cancellationToken);

        Task TouchLastSeenAsync(
            Guid connectorId,
            DateTimeOffset now,
            CancellationToken cancellationToken);

        Task<Result<PrintConnectorResponse>> CreateAsync(
            Guid branchId,
            string name,
            string keyHash,
            Guid actorUserId,
            CancellationToken cancellationToken);

        Task<IReadOnlyList<PrintConnectorResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken);

        Task<Result<PrintConnectorResponse>> RevokeAsync(
            Guid connectorId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken);
    }
}
