namespace CafeHub.Application.Provisioning
{
    public sealed class ProvisioningRetryOptions
    {
        public int MaxAttempts { get; set; } = 5;

        public int BaseSeconds { get; set; } = 30;

        public int MaxSeconds { get; set; } = 900;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (MaxAttempts < 1 || MaxAttempts > 20)
            {
                problems.Add($"{ProvisioningRetryConfigurationKeys.SectionName}:{ProvisioningRetryConfigurationKeys.MaxAttemptsKey} must be between 1 and 20.");
            }

            if (BaseSeconds < 1 || BaseSeconds > 3600)
            {
                problems.Add($"{ProvisioningRetryConfigurationKeys.SectionName}:{ProvisioningRetryConfigurationKeys.BaseSecondsKey} must be between 1 and 3600.");
            }

            if (MaxSeconds < BaseSeconds)
            {
                problems.Add($"{ProvisioningRetryConfigurationKeys.SectionName}:{ProvisioningRetryConfigurationKeys.MaxSecondsKey} must not be below {ProvisioningRetryConfigurationKeys.SectionName}:{ProvisioningRetryConfigurationKeys.BaseSecondsKey}.");
            }

            return problems;
        }
    }
}
