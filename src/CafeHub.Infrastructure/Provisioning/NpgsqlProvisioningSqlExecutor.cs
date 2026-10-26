using Npgsql;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Npgsql implementation of the provisioning statement executor. Statements run in auto-commit
    /// mode (CREATE DATABASE cannot run inside a transaction block); no statement text, connection
    /// string, or secret value is ever logged.
    /// </summary>
    internal sealed class NpgsqlProvisioningSqlExecutor : ITenantProvisioningSqlExecutor
    {
        private const string MaintenanceDatabaseName = "postgres";

        private readonly string _adminConnectionString;

        public NpgsqlProvisioningSqlExecutor(TenantDatabaseCreatorOptions options)
        {
            _adminConnectionString = options.AdminConnectionString;
        }

        public async Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken)
        {
            await using var connection = await OpenMaintenanceConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT 1 FROM pg_roles WHERE rolname = @roleName",
                connection);
            command.Parameters.AddWithValue("roleName", roleName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        public async Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken)
        {
            await using var connection = await OpenMaintenanceConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname = @databaseName",
                connection);
            command.Parameters.AddWithValue("databaseName", databaseName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null;
        }

        public async Task ExecuteClusterStatementAsync(string statement, CancellationToken cancellationToken)
        {
            await using var connection = await OpenMaintenanceConnectionAsync(cancellationToken);
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task ExecuteTenantStatementAsync(string databaseName, string statement, CancellationToken cancellationToken)
        {
            await using var connection = await OpenTenantConnectionAsync(databaseName, cancellationToken);
            await using var command = new NpgsqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<NpgsqlConnection> OpenMaintenanceConnectionAsync(CancellationToken cancellationToken)
        {
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = MaintenanceDatabaseName,
            };

            var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private async Task<NpgsqlConnection> OpenTenantConnectionAsync(string databaseName, CancellationToken cancellationToken)
        {
            var builder = new NpgsqlConnectionStringBuilder(_adminConnectionString)
            {
                Database = databaseName,
            };

            var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
