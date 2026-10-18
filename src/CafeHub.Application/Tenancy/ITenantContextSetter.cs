namespace CafeHub.Application.Tenancy
{
    public interface ITenantContextSetter
    {
        void Set(TenantContext tenantContext);
    }
}
