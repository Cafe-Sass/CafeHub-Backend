namespace CafeHub.Domain.Identity
{
    public static class PlatformAdminEmail
    {
        public static string Normalize(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("The platform admin email is required.", nameof(email));
            }

            return email.Trim().ToLowerInvariant();
        }
    }
}
