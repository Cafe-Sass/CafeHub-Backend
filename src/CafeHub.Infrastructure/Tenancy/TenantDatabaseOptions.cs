using Npgsql;

namespace CafeHub.Infrastructure.Tenancy
{
    public sealed class TenantDatabaseOptions
    {
        public string Host { get; set; } = string.Empty;

        public int Port { get; set; } = 5432;

        public SslMode SslMode { get; set; } = SslMode.Prefer;

        public int MaxPoolSize { get; set; } = 10;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrWhiteSpace(Host))
            {
                problems.Add("TenantDatabases:Host is required.");
            }

            if (Port is < 1 or > 65535)
            {
                problems.Add("TenantDatabases:Port must be between 1 and 65535.");
            }

            if (!Enum.IsDefined(SslMode))
            {
                problems.Add("TenantDatabases:SslMode must be a supported Npgsql SSL mode.");
            }

            if (MaxPoolSize is < 1 or > 100)
            {
                problems.Add("TenantDatabases:MaxPoolSize must be between 1 and 100.");
            }

            return problems;
        }
    }
}
