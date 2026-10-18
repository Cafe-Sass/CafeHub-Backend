using CafeHub.Application.Tenancy;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Tenancy
{
    internal sealed class ConfigurationSecretReferenceResolver(IConfiguration configuration) : ISecretReferenceResolver
    {
        public string Resolve(string secretReference)
        {
            var settingKey = $"{ConfigurationKeys.TenantConnectionsPrefix}{secretReference}";
            var connectionString = configuration[settingKey];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new TenantConnectionNotConfiguredException(
                    $"No tenant connection is configured for the setting \"{settingKey}\". Configure it with the environment variable prefix \"{ConfigurationKeys.TenantConnectionsEnvironmentVariablePrefix}\".");
            }

            return connectionString;
        }
    }
}
