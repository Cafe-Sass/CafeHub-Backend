namespace CafeHub.Application.Tenancy
{
    public sealed class InvalidTenantContextException(string message) : TenantIsolationException(message)
    {
    }
}
