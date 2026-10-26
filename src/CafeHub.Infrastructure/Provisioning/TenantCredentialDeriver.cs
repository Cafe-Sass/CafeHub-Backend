using System.Security.Cryptography;
using System.Text;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Derives the deterministic per-tenant role credential from the provisioning master key.
    /// The derived value is a secret: it must never be logged, returned in an error, or embedded
    /// anywhere except the role password statements themselves.
    /// </summary>
    public sealed class TenantCredentialDeriver
    {
        public string Derive(byte[] masterKey, Guid tenantId)
        {
            ArgumentNullException.ThrowIfNull(masterKey);
            if (masterKey.Length == 0)
            {
                throw new ArgumentException("The master key is required.", nameof(masterKey));
            }

            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(tenantId));
            }

            using var hmac = new HMACSHA256(masterKey);
            return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes($"tenant-db:{tenantId}")));
        }

        private static string Base64UrlEncode(byte[] value)
        {
            return Convert.ToBase64String(value)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
