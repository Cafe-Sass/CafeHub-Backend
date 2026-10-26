using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Provisioning
{
    public static class ProvisioningWorkerOptionsBinder
    {
        public static ProvisioningWorkerOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(ProvisioningWorkerConfigurationKeys.SectionName);

            var options = new ProvisioningWorkerOptions
            {
                Enabled = ReadBoolean(section, ProvisioningWorkerConfigurationKeys.EnabledKey),
                PollIntervalSeconds = ReadInt32(section, ProvisioningWorkerConfigurationKeys.PollIntervalSecondsKey, fallback: 5),
                LeaseSeconds = ReadInt32(section, ProvisioningWorkerConfigurationKeys.LeaseSecondsKey, fallback: 300),
                WorkerId = string.IsNullOrWhiteSpace(section?[ProvisioningWorkerConfigurationKeys.WorkerIdKey])
                    ? $"{Environment.MachineName}-{Guid.CreateVersion7():N}"
                    : section[ProvisioningWorkerConfigurationKeys.WorkerIdKey]!,
            };

            var problems = options.Validate();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid {ProvisioningWorkerConfigurationKeys.SectionName} configuration: {string.Join("; ", problems)}");
            }

            return options;
        }

        private static bool ReadBoolean(IConfiguration? section, string key)
        {
            var raw = section?[key];
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            if (bool.TryParse(raw, out var value))
            {
                return value;
            }

            throw new InvalidOperationException(
                $"{ProvisioningWorkerConfigurationKeys.SectionName}:{key} must be a boolean value.");
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
                $"{ProvisioningWorkerConfigurationKeys.SectionName}:{key} must be an integer value.");
        }
    }
}
