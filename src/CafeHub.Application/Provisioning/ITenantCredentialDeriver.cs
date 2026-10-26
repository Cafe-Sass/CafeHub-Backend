namespace CafeHub.Application.Provisioning
{
    public interface ITenantCredentialDeriver
    {
        string Derive(byte[] masterKey, Guid tenantId);
    }
}
