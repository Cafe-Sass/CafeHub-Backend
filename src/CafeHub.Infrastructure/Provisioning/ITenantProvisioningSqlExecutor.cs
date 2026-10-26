namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Executes the provisioning statements against the PostgreSQL cluster. Cluster-level operations
    /// (catalog checks, roles, database creation) run on the maintenance database; per-database
    /// privilege statements run connected to the target database.
    /// </summary>
    internal interface ITenantProvisioningSqlExecutor
    {
        Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken);

        Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken);

        Task ExecuteClusterStatementAsync(string statement, CancellationToken cancellationToken);

        Task ExecuteTenantStatementAsync(string databaseName, string statement, CancellationToken cancellationToken);
    }
}
