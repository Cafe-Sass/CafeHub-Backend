using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Persistence.Master;

public sealed class MasterConnectionStringResolver(IConfiguration configuration)
{
    public string Resolve()
    {
        var connectionString = configuration[ConfigurationKeys.MasterConnectionString];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new MasterDatabaseNotConfiguredException(
                $"The master database connection string is not configured. Set it with User Secrets as \"{ConfigurationKeys.MasterConnectionString}\" or with the environment variable \"{ConfigurationKeys.MasterConnectionStringEnvironmentVariable}\".");
        }

        return connectionString;
    }
}
