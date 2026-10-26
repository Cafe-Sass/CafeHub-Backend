using Microsoft.Extensions.Configuration;
using Npgsql;

namespace CafeHub.Infrastructure.Tenancy
{
    public static class TenantDatabaseOptionsBinder
    {
        public static TenantDatabaseOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection("TenantDatabases");
            var options = new TenantDatabaseOptions
            {
                Host = section?["Host"] ?? string.Empty,
                Port = ReadInt32(section, "Port", 5432),
                MaxPoolSize = ReadInt32(section, "MaxPoolSize", 10),
                SslMode = ReadSslMode(section?["SslMode"]),
            };

            var problems = options.Validate();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Invalid TenantDatabases configuration: {string.Join("; ", problems)}");
            }

            return options;
        }

        private static int ReadInt32(IConfigurationSection? section, string key, int fallback)
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

            throw new InvalidOperationException($"TenantDatabases:{key} must be an integer value.");
        }

        private static SslMode ReadSslMode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return SslMode.Prefer;
            }

            if (Enum.TryParse<SslMode>(raw, ignoreCase: true, out var value) && Enum.IsDefined(value))
            {
                return value;
            }

            throw new InvalidOperationException("TenantDatabases:SslMode must be a supported Npgsql SSL mode.");
        }
    }
}
