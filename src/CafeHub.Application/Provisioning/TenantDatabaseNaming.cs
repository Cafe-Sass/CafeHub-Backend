using System.Text.RegularExpressions;

namespace CafeHub.Application.Provisioning
{
    /// <summary>
    /// Derives the PostgreSQL database and role names for a tenant from its slug, exactly as the
    /// reviewed PostgreSQL operations commands define them. One function, deterministic per slug.
    /// </summary>
    public static partial class TenantDatabaseNaming
    {
        public const string DatabaseNamePattern = "^[a-z][a-z0-9_]{0,62}$";

        public const string DatabasePrefix = "cafehub_tenant_";
        public const string OwnerRoleSuffix = "_owner";
        public const string ApplicationRoleSuffix = "_app";

        [GeneratedRegex(DatabaseNamePattern)]
        private static partial Regex DatabaseNameRegex();

        [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$")]
        private static partial Regex TenantSlugRegex();

        public static bool IsValidDatabaseName(string databaseName)
        {
            return !string.IsNullOrEmpty(databaseName) && DatabaseNameRegex().IsMatch(databaseName);
        }

        public static bool IsValidTenantSlug(string tenantSlug)
        {
            return !string.IsNullOrEmpty(tenantSlug) && TenantSlugRegex().IsMatch(tenantSlug);
        }

        public static string DatabaseName(string tenantSlug)
        {
            return DatabasePrefix + tenantSlug.Replace("-", "_");
        }

        public static string OwnerRoleName(string databaseName)
        {
            return databaseName + OwnerRoleSuffix;
        }

        public static string ApplicationRoleName(string databaseName)
        {
            return databaseName + ApplicationRoleSuffix;
        }
    }
}
