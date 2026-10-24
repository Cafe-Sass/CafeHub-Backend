using System.Security.Cryptography;

namespace CafeHub.Infrastructure.Authentication
{
    internal sealed class RefreshTokenGenerator : IRefreshTokenGenerator
    {
        private const int TokenBytes = 32;

        public string Generate()
        {
            var bytes = RandomNumberGenerator.GetBytes(TokenBytes);

            return Base64UrlEncoder(bytes);
        }

        private static string Base64UrlEncoder(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
