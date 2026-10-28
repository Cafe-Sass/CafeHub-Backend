namespace CafeHub.Domain.Tenants.Credentials
{
    /// <summary>
    /// Identifies the credential derivation scheme and tenant scope stored with a tenant database.
    /// </summary>
    public static class TenantDatabaseCredentialReference
    {
        public const string LegacyV1 = "v1";

        public static string CreateV1(Guid tenantId)
        {
            if (tenantId == Guid.Empty)
            {
                throw new ArgumentException("The tenant id is required.", nameof(tenantId));
            }

            return $"v1-{tenantId:N}";
        }

        public static bool IsValid(string? reference)
        {
            if (string.IsNullOrEmpty(reference)
                || reference.Length > 128
                || reference[0] is not (>= 'a' and <= 'z' or >= '0' and <= '9'))
            {
                return false;
            }

            foreach (var character in reference)
            {
                if (character is not (>= 'a' and <= 'z' or >= '0' and <= '9' or '-'))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsSupportedForTenant(string? reference, Guid tenantId)
        {
            return string.Equals(reference, LegacyV1, StringComparison.Ordinal)
                || (tenantId != Guid.Empty
                    && string.Equals(reference, CreateV1(tenantId), StringComparison.Ordinal));
        }
    }
}
