namespace CafeHub.Infrastructure.Provisioning
{
    public sealed class TenantDatabaseCreatorOptions
    {
        public string AdminConnectionString { get; set; } = string.Empty;

        public string CredentialMasterKey { get; set; } = string.Empty;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(AdminConnectionString))
            {
                problems.Add($"{TenantDatabaseCreatorConfigurationKeys.SectionName}:{TenantDatabaseCreatorConfigurationKeys.AdminConnectionStringKey} is required.");
            }

            if (string.IsNullOrWhiteSpace(CredentialMasterKey))
            {
                problems.Add($"{TenantDatabaseCreatorConfigurationKeys.SectionName}:{TenantDatabaseCreatorConfigurationKeys.CredentialMasterKeyKey} is required.");
            }

            return problems;
        }
    }
}
