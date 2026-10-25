namespace CafeHub.Infrastructure.Identity
{
    public static class IdentityConfigurationKeys
    {
        public const string SectionName = "Identity";
        public const string LockoutKey = "Lockout";
        public const string MaxFailedAccessAttemptsKey = "MaxFailedAccessAttempts";
        public const string LockoutMinutesKey = "LockoutMinutes";

        public const string MaxFailedAccessAttemptsPath = $"{SectionName}:{LockoutKey}:{MaxFailedAccessAttemptsKey}";
        public const string LockoutMinutesPath = $"{SectionName}:{LockoutKey}:{LockoutMinutesKey}";
    }
}
