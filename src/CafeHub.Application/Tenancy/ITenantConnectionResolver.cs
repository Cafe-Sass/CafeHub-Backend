namespace CafeHub.Application.Tenancy
{
    public interface ITenantConnectionResolver
    {
        string Resolve(TenantContext tenant);
    }
}
