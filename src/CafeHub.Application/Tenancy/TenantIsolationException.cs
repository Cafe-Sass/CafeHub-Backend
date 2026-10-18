namespace CafeHub.Application.Tenancy
{
    public abstract class TenantIsolationException(string message) : Exception(message)
    {
    }
}
