using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tenancy
{
    public interface ITenantStateGuard
    {
        Result EnsureUsable(ResolvedTenant tenant);
    }
}
