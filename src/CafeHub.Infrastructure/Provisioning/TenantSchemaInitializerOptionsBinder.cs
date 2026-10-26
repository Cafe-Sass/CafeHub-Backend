using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Provisioning
{
    public static class TenantSchemaInitializerOptionsBinder
    {
        public static TenantSchemaInitializerOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(TenantSchemaInitializerConfigurationKeys.SectionName);

            var options = new TenantSchemaInitializerOptions
            {
                AdminConnectionString = section?[TenantSchemaInitializerConfigurationKeys.AdminConnectionStringKey] ?? string.Empty,
            };

            var problems = options.Validate();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid {TenantSchemaInitializerConfigurationKeys.SectionName} configuration: {string.Join("; ", problems)}");
            }

            return options;
        }
    }
}
