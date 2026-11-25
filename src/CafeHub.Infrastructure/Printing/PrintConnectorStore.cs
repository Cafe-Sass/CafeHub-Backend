using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Audit;
using CafeHub.Application.Printing;
using CafeHub.Application.Printing.Contracts;
using CafeHub.Application.Printing.DTOs;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Audit;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CafeHub.Infrastructure.Printing
{
    internal sealed class PrintConnectorStore(
        TenantDbContext context,
        ITenantAuditWriter auditWriter,
        TimeProvider timeProvider) : IPrintConnectorStore
    {
        private const string TargetType = "print_connectors";

        public async Task<PrintConnectorAccess?> FindByKeyHashAsync(
            string keyHash,
            CancellationToken cancellationToken) =>
            await context.PrintConnectors.AsNoTracking()
                .Where(connector => connector.KeyHash == keyHash)
                .Select(connector => new PrintConnectorAccess(
                    connector.Id,
                    connector.BranchId,
                    connector.Name,
                    connector.KeyHash,
                    connector.Status))
                .FirstOrDefaultAsync(cancellationToken);

        public async Task TouchLastSeenAsync(
            Guid connectorId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var connector = await context.PrintConnectors
                .FirstOrDefaultAsync(candidate => candidate.Id == connectorId, cancellationToken);
            if (connector is null)
            {
                return;
            }

            connector.MarkSeen(now);
            await context.SaveChangesAsync(cancellationToken);
        }

        public async Task<Result<PrintConnectorResponse>> CreateAsync(
            Guid branchId,
            string name,
            string keyHash,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var branchExists = await context.Branches.AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId, cancellationToken);
            if (!branchExists)
            {
                return Result.Failure<PrintConnectorResponse>(PrintingErrors.NotFound());
            }

            var now = timeProvider.GetUtcNow();
            var connector = PrintConnector.Create(
                Guid.CreateVersion7(),
                branchId,
                name,
                keyHash,
                now,
                actorUserId);
            context.PrintConnectors.Add(connector);

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "print_connectors.created.succeeded",
                TargetType = TargetType,
                TargetId = connector.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Create(new[]
                {
                    new TenantAuditChanges.FieldChange(nameof(PrintConnector.Name), null, connector.Name),
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintConnector.BranchId), null, connector.BranchId.ToString()),
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintConnector.Status), null, connector.Status.ToString()),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<PrintConnectorResponse>(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintConnectorResponse.From(connector));
        }

        public async Task<IReadOnlyList<PrintConnectorResponse>> ListAsync(
            Guid branchId,
            CancellationToken cancellationToken)
        {
            var connectors = await context.PrintConnectors.AsNoTracking()
                .Where(connector => connector.BranchId == branchId)
                .OrderBy(connector => connector.Name)
                .ThenBy(connector => connector.Id)
                .ToListAsync(cancellationToken);

            return connectors.Select(PrintConnectorResponse.From).ToArray();
        }

        public async Task<Result<PrintConnectorResponse>> RevokeAsync(
            Guid connectorId,
            Guid branchId,
            Guid actorUserId,
            CancellationToken cancellationToken)
        {
            var connector = await context.PrintConnectors.FirstOrDefaultAsync(
                candidate => candidate.Id == connectorId && candidate.BranchId == branchId,
                cancellationToken);
            if (connector is null)
            {
                return Result.Failure<PrintConnectorResponse>(PrintingErrors.ConnectorNotFound());
            }

            if (!connector.Revoke(timeProvider.GetUtcNow(), actorUserId))
            {
                return Result.Success(PrintConnectorResponse.From(connector));
            }

            var audit = await auditWriter.WriteAsync(new TenantAuditEntry
            {
                Action = "print_connectors.revoked.succeeded",
                TargetType = TargetType,
                TargetId = connector.Id,
                ActorUserId = actorUserId,
                Changes = TenantAuditChanges.Update(new[]
                {
                    new TenantAuditChanges.FieldChange(
                        nameof(PrintConnector.Status),
                        PrintConnectorStatus.Active.ToString(),
                        connector.Status.ToString()),
                }),
            }, cancellationToken);
            if (audit.IsFailure)
            {
                return Result.Failure<PrintConnectorResponse>(audit.Error!);
            }

            await context.SaveChangesAsync(cancellationToken);
            return Result.Success(PrintConnectorResponse.From(connector));
        }
    }
}
