namespace CafeHub.Infrastructure.Upgrades
{
    public static class TenantSchemaUpgradeWorkerConfigurationKeys
    {
        public const string SectionName = "Upgrades:Worker";

        public const string EnabledKey = "Enabled";
        public const string PollIntervalSecondsKey = "PollIntervalSeconds";
        public const string LeaseSecondsKey = "LeaseSeconds";
        public const string WorkerIdKey = "WorkerId";
    }
}
