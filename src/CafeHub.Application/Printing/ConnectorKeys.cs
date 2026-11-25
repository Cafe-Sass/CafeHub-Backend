using System.Security.Cryptography;
using System.Text;

namespace CafeHub.Application.Printing
{
    /// <summary>
    /// Creates connector keys (returned once) and their stored SHA-256 form. A presented key is only ever
    /// compared through <see cref="Matches"/>, which runs in constant time; the raw key is never stored,
    /// logged, or audited.
    /// </summary>
    public static class ConnectorKeys
    {
        public const int KeyLength = 64;
        public const int KeyHashLength = 64;

        public static string Generate() =>
            Convert.ToHexString(RandomNumberGenerator.GetBytes(KeyLength / 2)).ToLowerInvariant();

        public static string Hash(string key) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

        public static bool IsWellFormed(string? key) =>
            key is { Length: KeyLength } && key.All(char.IsAsciiHexDigit);

        public static bool Matches(string storedHash, string presentedHash) =>
            storedHash.Length == presentedHash.Length
            && CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(storedHash),
                Encoding.ASCII.GetBytes(presentedHash));
    }
}
