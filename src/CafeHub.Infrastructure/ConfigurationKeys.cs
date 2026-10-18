namespace CafeHub.Infrastructure
{
    public static class ConfigurationKeys
    {
        public const string MasterConnectionString = "ConnectionStrings:Master";
        public const string MasterConnectionStringEnvironmentVariable = "ConnectionStrings__Master";
        public const string TenantConnectionsPrefix = "TenantConnections:";
        public const string TenantConnectionsEnvironmentVariablePrefix = "TenantConnections__";
        public const string TenantDesignTimeConnectionString = "ConnectionStrings:TenantDesignTime";
        public const string TenantDesignTimeConnectionStringEnvironmentVariable = "ConnectionStrings__TenantDesignTime";
    }
}
