using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tenancy
{
    public static class TenancyErrors
    {
        public const string TenantUnavailableCode = "Tenant.Unavailable";
        public const string TenantUnavailableMessage = "The requested tenant is not available.";

        public static Error TenantUnavailable()
        {
            return Error.NotFound(TenantUnavailableCode, TenantUnavailableMessage);
        }
    }
}
