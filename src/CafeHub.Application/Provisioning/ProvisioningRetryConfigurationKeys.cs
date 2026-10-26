namespace CafeHub.Application.Provisioning
{
    public static class ProvisioningRetryConfigurationKeys
    {
        public const string SectionName = "Provisioning:Retry";

        public const string MaxAttemptsKey = "MaxAttempts";
        public const string BaseSecondsKey = "BaseSeconds";
        public const string MaxSecondsKey = "MaxSeconds";
    }
}
