namespace CafeHub.Infrastructure.Identity
{
    public static class PlatformSeedConfigurationKeys
    {
        public const string SectionName = "PlatformSeed";
        public const string SuperAdminEmailKey = "SuperAdminEmail";
        public const string SuperAdminPasswordKey = "SuperAdminPassword";

        public const string SuperAdminEmailPath = $"{SectionName}:{SuperAdminEmailKey}";
        public const string SuperAdminPasswordPath = $"{SectionName}:{SuperAdminPasswordKey}";
    }
}
