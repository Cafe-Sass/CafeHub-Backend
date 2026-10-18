namespace CafeHub.Application.Tenancy
{
    public sealed class TenantConnectionNotConfiguredException(string message) : TenantIsolationException(message)
    {
    }
}
