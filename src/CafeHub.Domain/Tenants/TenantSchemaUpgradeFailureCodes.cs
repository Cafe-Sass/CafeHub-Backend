namespace CafeHub.Domain.Tenants
{
    public static class TenantSchemaUpgradeFailureCodes
    {
        public const string TenantNotReady = "Upgrade.TenantNotReady";

        public const string ConnectionFailed = "Upgrade.ConnectionFailed";

        public const string MigrationFailed = "Upgrade.MigrationFailed";

        public const string Unexpected = "Upgrade.Unexpected";

        private static readonly HashSet<string> Codes = new(
            new[]
            {
                TenantNotReady,
                ConnectionFailed,
                MigrationFailed,
                Unexpected,
            },
            StringComparer.Ordinal);

        public static IReadOnlyCollection<string> All => Codes;

        public static bool IsDefined(string? code)
        {
            return code is not null && Codes.Contains(code);
        }
    }
}
