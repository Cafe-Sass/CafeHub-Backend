namespace CafeHub.Application.Tenancy
{
    public sealed class TenantContextMissingException(string message) : TenantIsolationException(message)
    {
    }
}
