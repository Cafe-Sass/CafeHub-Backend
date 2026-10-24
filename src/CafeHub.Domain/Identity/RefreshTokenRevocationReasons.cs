namespace CafeHub.Domain.Identity
{
    public static class RefreshTokenRevocationReasons
    {
        public const string Logout = "logout";

        public const string ReuseDetected = "reuse_detected";

        public const string AdminRevoked = "admin_revoked";

        public static bool IsDefined(string reason)
        {
            return reason is Logout or ReuseDetected or AdminRevoked;
        }
    }
}
