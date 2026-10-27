using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Audit;

namespace CafeHub.Application.Identity.Commands
{
    internal static class TenantAuthenticationAudit
    {
        public static async Task WriteAndCommitAsync(
            ITenantAuditWriter auditWriter,
            ITenantUnitOfWork unitOfWork,
            TenantAuditEntry entry,
            CancellationToken cancellationToken)
        {
            var result = await auditWriter.WriteAsync(entry, cancellationToken);
            if (result.IsFailure)
            {
                throw new InvalidOperationException("The tenant audit entry could not be written.");
            }

            await unitOfWork.BeginTransactionAsync(cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);
        }
    }
}
