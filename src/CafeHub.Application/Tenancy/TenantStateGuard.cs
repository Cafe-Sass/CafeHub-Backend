using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tenancy
{
    public sealed class TenantStateGuard : ITenantStateGuard
    {
        public Result EnsureUsable(ResolvedTenant tenant)
        {
            ArgumentNullException.ThrowIfNull(tenant);

            return TenantStateAvailability.IsUsable(tenant.State)
                && !string.IsNullOrWhiteSpace(tenant.SecretReference)
                ? Result.Success()
                : Result.Failure(TenancyErrors.TenantUnavailable());
        }
    }
}
