using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Tenancy
{
    public static class TenancyErrors
    {
        public const string TenantUnavailableCode = "Tenant.Unavailable";
        public const string TenantUnavailableMessage = "The requested tenant is not available.";

        private static readonly Error Unavailable = Error.NotFound(TenantUnavailableCode, TenantUnavailableMessage);

        public static Error TenantUnavailable()
        {
            return Unavailable;
        }
    }
}
