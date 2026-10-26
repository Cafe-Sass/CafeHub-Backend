using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Provisioning
{
    public static class TenantDatabaseCreatorOptionsBinder
    {
        private const int MinimumMasterKeyBytes = 32;

        public static TenantDatabaseCreatorOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(TenantDatabaseCreatorConfigurationKeys.SectionName);

            var options = new TenantDatabaseCreatorOptions
            {
                AdminConnectionString = section?[TenantDatabaseCreatorConfigurationKeys.AdminConnectionStringKey] ?? string.Empty,
                CredentialMasterKey = section?[TenantDatabaseCreatorConfigurationKeys.CredentialMasterKeyKey] ?? string.Empty,
            };

            var problems = new List<string>(options.Validate());
            if (!string.IsNullOrWhiteSpace(options.CredentialMasterKey))
            {
                var masterKeyProblem = ValidateMasterKey(options.CredentialMasterKey);
                if (masterKeyProblem is not null)
                {
                    problems.Add(masterKeyProblem);
                }
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid {TenantDatabaseCreatorConfigurationKeys.SectionName} configuration: {string.Join("; ", problems)}");
            }

            return options;
        }

        private static string? ValidateMasterKey(string credentialMasterKey)
        {
            byte[] key;
            try
            {
                key = Convert.FromBase64String(credentialMasterKey);
            }
            catch (FormatException)
            {
                return $"{TenantDatabaseCreatorConfigurationKeys.SectionName}:{TenantDatabaseCreatorConfigurationKeys.CredentialMasterKeyKey} must be a base64 encoded key.";
            }

            if (key.Length < MinimumMasterKeyBytes)
            {
                return $"{TenantDatabaseCreatorConfigurationKeys.SectionName}:{TenantDatabaseCreatorConfigurationKeys.CredentialMasterKeyKey} must decode to at least {MinimumMasterKeyBytes} bytes.";
            }

            return null;
        }
    }
}
