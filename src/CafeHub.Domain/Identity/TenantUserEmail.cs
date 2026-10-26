namespace CafeHub.Domain.Identity
{
    /// <summary>
    /// Normalization rules for tenant user email addresses, mirroring
    /// <see cref="PlatformAdminEmail"/> for the Master side. Tenant users store both the
    /// original-case email and a normalized companion column, exactly as the Identity
    /// mechanism is designed to be used.
    /// </summary>
    public static class TenantUserEmail
    {
        public static string Normalize(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("The tenant user email is required.", nameof(email));
            }

            return email.Trim().ToLowerInvariant();
        }
    }
}
