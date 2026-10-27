using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Audit
{
    public interface ITenantAuditWriter
    {
        Task<Result> WriteAsync(TenantAuditEntry entry, CancellationToken cancellationToken);
    }
}
