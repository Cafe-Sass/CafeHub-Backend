using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tenancy
{
    public interface ITenantResolver
    {
        Task<Result<ResolvedTenant>> ResolveAsync(string rawSlug, CancellationToken cancellationToken);
    }
}
