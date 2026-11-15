using System.Security.Cryptography;
using System.Text;

namespace CafeHub.Application.Identity
{
    public static class TenantSessionVersion
    {
        public const string Claim = "staff_session_version";

        public static string FromStamp(string stamp) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stamp)));
    }
}
