namespace CafeHub.Infrastructure.Provisioning
{
    public sealed class TenantSchemaInitializerOptions
    {
        public string AdminConnectionString { get; set; } = string.Empty;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(AdminConnectionString))
            {
                problems.Add($"{TenantSchemaInitializerConfigurationKeys.SectionName}:{TenantSchemaInitializerConfigurationKeys.AdminConnectionStringKey} is required.");
            }

            return problems;
        }
    }
}
