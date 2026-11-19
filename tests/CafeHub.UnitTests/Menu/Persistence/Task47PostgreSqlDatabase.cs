using CafeHub.Application.Provisioning;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CafeHub.UnitTests.Menu.Persistence
{
    internal sealed class Task47PostgreSqlDatabase(NpgsqlConnectionStringBuilder connection, string maintenance) : IAsyncDisposable
    {
        public string Name => connection.Database!;
        public string ApplicationRole => TenantDatabaseNaming.ApplicationRoleName(Name);
        public string ConnectionString => connection.ConnectionString;

        public TenantDbContext CreateContext() => new(new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(ConnectionString).Options);

        public NpgsqlConnection CreateConnection() => new(ConnectionString);

        public static async Task<Task47PostgreSqlDatabase> CreateAsync()
        {
            var connection = new NpgsqlConnectionStringBuilder(
                Environment.GetEnvironmentVariable(Task47PostgreSqlFactAttribute.ConnectionVariable));
            Assert.Contains(connection.Host, new[] { "127.0.0.1", "localhost", "::1" });
            Assert.StartsWith("cafehub_task47_disposable_", connection.Database, StringComparison.Ordinal);
            connection.Pooling = false;
            connection.CommandTimeout = 25;
            var maintenance = new NpgsqlConnectionStringBuilder(connection.ConnectionString) { Database = "postgres" };
            connection.Database = "cafehub_task47_disposable_" + Guid.NewGuid().ToString("N")[..16];
            var database = new Task47PostgreSqlDatabase(connection, maintenance.ConnectionString);
            await using var admin = await NpgsqlConnectionExtensions.OpenAsync(maintenance.ConnectionString);
            try
            {
                await ExecuteAsync(admin, $"CREATE ROLE {Quote(database.ApplicationRole)} NOLOGIN");
                await ExecuteAsync(admin, $"CREATE DATABASE {Quote(database.Name)}");
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await using var admin = await NpgsqlConnectionExtensions.OpenAsync(maintenance);
            await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS {Quote(Name)}");
            await ExecuteAsync(admin, $"DROP ROLE IF EXISTS {Quote(ApplicationRole)}");
        }

        public static string Quote(string identifier) => new NpgsqlCommandBuilder().QuoteIdentifier(identifier);

        public static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static class NpgsqlConnectionExtensions
        {
            public static async Task<NpgsqlConnection> OpenAsync(string connectionString)
            {
                var connection = new NpgsqlConnection(connectionString);
                try
                {
                    await connection.OpenAsync();
                    return connection;
                }
                catch
                {
                    await connection.DisposeAsync();
                    throw;
                }
            }
        }
    }
}
