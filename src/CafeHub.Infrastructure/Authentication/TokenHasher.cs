using System.Security.Cryptography;
using System.Text;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class TokenHasher : ITokenHasher
    {
        public string Hash(string rawToken)
        {
            ArgumentNullException.ThrowIfNull(rawToken);

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}
