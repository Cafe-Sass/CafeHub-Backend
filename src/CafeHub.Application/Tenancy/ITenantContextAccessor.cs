namespace CafeHub.Application.Tenancy
{
    public interface ITenantContextAccessor
    {
        TenantContext GetRequired();
    }
}
