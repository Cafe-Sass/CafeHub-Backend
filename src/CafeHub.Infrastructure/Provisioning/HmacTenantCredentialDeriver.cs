using System.Security.Cryptography;
using System.Text;
using CafeHub.Application.Provisioning;

namespace CafeHub.Infrastructure.Provisioning
{
    public sealed class HmacTenantCredentialDeriver : ITenantCredentialDeriver
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
            var input = Encoding.UTF8.GetBytes($"tenant-db:{tenantId}");
            return Convert.ToBase64String(hmac.ComputeHash(input))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }
    }
}
