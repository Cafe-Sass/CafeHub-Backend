using CafeHub.Application.Provisioning;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Derives the deterministic per-tenant role credential from the provisioning master key.
    /// The derived value is a secret: it must never be logged, returned in an error, or embedded
    /// anywhere except the role password statements themselves.
    /// </summary>
    public sealed class TenantCredentialDeriver : ITenantCredentialDeriver
    {
        public string Derive(byte[] masterKey, Guid tenantId)
        {
            return new HmacTenantCredentialDeriver().Derive(masterKey, tenantId);
        }
    }
}
