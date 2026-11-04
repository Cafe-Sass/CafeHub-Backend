using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Upgrades
{
    public static class TenantSchemaUpgradeWorkerOptionsBinder
    {
        public static TenantSchemaUpgradeWorkerOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(TenantSchemaUpgradeWorkerConfigurationKeys.SectionName);

            var options = new TenantSchemaUpgradeWorkerOptions
            {
                Enabled = ReadBoolean(section, TenantSchemaUpgradeWorkerConfigurationKeys.EnabledKey),
                PollIntervalSeconds = ReadInt32(section, TenantSchemaUpgradeWorkerConfigurationKeys.PollIntervalSecondsKey, fallback: 5),
                LeaseSeconds = ReadInt32(section, TenantSchemaUpgradeWorkerConfigurationKeys.LeaseSecondsKey, fallback: 300),
                WorkerId = string.IsNullOrWhiteSpace(section?[TenantSchemaUpgradeWorkerConfigurationKeys.WorkerIdKey])
                    ? $"{Environment.MachineName}-{Guid.CreateVersion7():N}"
                    : section[TenantSchemaUpgradeWorkerConfigurationKeys.WorkerIdKey]!,
            };

            var problems = options.Validate();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid {TenantSchemaUpgradeWorkerConfigurationKeys.SectionName} configuration: {string.Join("; ", problems)}");
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
                $"{TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{key} must be a boolean value.");
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
                $"{TenantSchemaUpgradeWorkerConfigurationKeys.SectionName}:{key} must be an integer value.");
        }
    }
}
