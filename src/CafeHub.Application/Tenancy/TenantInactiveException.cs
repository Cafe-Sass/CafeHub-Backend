namespace CafeHub.Application.Tenancy
{
    public sealed class TenantInactiveException(string message) : TenantIsolationException(message)
    {
    }
}
