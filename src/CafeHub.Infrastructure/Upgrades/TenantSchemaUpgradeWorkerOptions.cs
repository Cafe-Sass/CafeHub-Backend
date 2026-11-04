namespace CafeHub.Infrastructure.Upgrades
{
    public sealed class TenantSchemaUpgradeWorkerOptions
    {
        public bool Enabled { get; set; } = false;

        public int PollIntervalSeconds { get; set; } = 5;

        public int LeaseSeconds { get; set; } = 300;

        public string WorkerId { get; set; } = string.Empty;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (PollIntervalSeconds < 1 || PollIntervalSeconds > 300)
            {
                problems.Add($"{TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey} must be between 1 and 300.");
            }

            if (LeaseSeconds < 30 || LeaseSeconds > 3600)
            {
                problems.Add($"{TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{TenantSchemaUpgradeWorkerConfigurationKeys.LeaseSecondsKey} must be between 30 and 3600.");
            }

            if (LeaseSeconds <= PollIntervalSeconds)
            {
                problems.Add($"{TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{TenantSchemaUpgradeWorkerConfigurationKeys.LeaseSecondsKey} must exceed {TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey}.");
            }

            return problems;
        }
    }
}
