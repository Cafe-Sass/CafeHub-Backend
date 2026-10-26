using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Provisioning
{
    public static class ProvisioningRetryOptionsBinder
    {
        private const int DefaultMaxAttempts = 5;
        private const int DefaultBaseSeconds = 30;
        private const int DefaultMaxSeconds = 900;

        public static ProvisioningRetryOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(ProvisioningRetryConfigurationKeys.SectionName);

            var options = new ProvisioningRetryOptions
            {
                MaxAttempts = ReadInt32(section, ProvisioningRetryConfigurationKeys.MaxAttemptsKey, DefaultMaxAttempts),
                BaseSeconds = ReadInt32(section, ProvisioningRetryConfigurationKeys.BaseSecondsKey, DefaultBaseSeconds),
                MaxSeconds = ReadInt32(section, ProvisioningRetryConfigurationKeys.MaxSecondsKey, DefaultMaxSeconds),
            };

            var problems = options.Validate();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid {ProvisioningRetryConfigurationKeys.SectionName} configuration: {string.Join("; ", problems)}");
            }

            return options;
        }

        private static int ReadInt32(IConfiguration? section, string key, int fallback)
        {
            var raw = section?[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return fallback;
            }

            if (int.TryParse(raw, out var value))
            {
                return value;
            }

            throw new InvalidOperationException(
                $"{ProvisioningRetryConfigurationKeys.SectionName}:{key} must be an integer value.");
        }
    }
}
