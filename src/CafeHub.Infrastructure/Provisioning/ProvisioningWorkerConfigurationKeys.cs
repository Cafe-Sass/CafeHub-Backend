namespace CafeHub.Infrastructure.Provisioning
{
    public static class ProvisioningWorkerConfigurationKeys
    {
        public const string SectionName = "Provisioning:Worker";

        public const string EnabledKey = "Enabled";
        public const string PollIntervalSecondsKey = "PollIntervalSeconds";
        public const string LeaseSecondsKey = "LeaseSeconds";
        public const string WorkerIdKey = "WorkerId";
    }
}

