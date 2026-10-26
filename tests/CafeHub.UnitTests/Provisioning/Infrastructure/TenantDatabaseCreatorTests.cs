using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using CafeHub.Infrastructure.Provisioning;
using Microsoft.Extensions.Logging;

namespace CafeHub.UnitTests.Provisioning.Infrastructure
{
    public sealed class TenantDatabaseCreatorTests
    {
        private static readonly Guid TenantId = Guid.CreateVersion7();
        private static readonly byte[] MasterKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        private static readonly TenantDatabaseSpec Spec = new(TenantId, "cool-cafe");
        private const string DatabaseName = "cafehub_tenant_cool_cafe";
        private const string OwnerRole = "cafehub_tenant_cool_cafe_owner";
        private const string ApplicationRole = "cafehub_tenant_cool_cafe_app";

        private readonly TenantCredentialDeriver _deriver = new();
        private readonly FakeResolver _resolver = new();
        private readonly CapturingLogger _logger = new();

        private TenantDatabaseCreator CreateCreator() => new(
            new TenantDatabaseCreatorOptions
            {
                AdminConnectionString = "Host=localhost;Database=postgres",
                CredentialMasterKey = Convert.ToBase64String(MasterKey),
            },
            _deriver,
            _resolver,
            _logger);

        [Fact]
        public async Task EnsureDatabaseAsync_WithMissingDatabaseAndRoles_CreatesRoleThenDatabaseThenPrivileges()
        {
            var result = await CreateCreator().EnsureDatabaseAsync(Spec, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Empty(_logger.Messages);

            var cluster = _resolver.ClusterStatements;

            var firstRole = cluster.FindIndex(statement => statement.StartsWith("CREATE ROLE ", StringComparison.Ordinal));
            var createDatabase = cluster.FindIndex(statement => statement.StartsWith("CREATE DATABASE ", StringComparison.Ordinal));
            var revokeDatabase = cluster.FindIndex(statement => statement.StartsWith("REVOKE ALL ON DATABASE", StringComparison.Ordinal));
            var grantConnect = cluster.FindIndex(statement => statement.StartsWith("GRANT CONNECT ON DATABASE", StringComparison.Ordinal));

            Assert.True(firstRole >= 0);
            Assert.True(createDatabase > firstRole);
            Assert.True(revokeDatabase > createDatabase);
            Assert.True(grantConnect > createDatabase);
            Assert.Contains(cluster, statement => statement.Contains($"\"{OwnerRole}\"", StringComparison.Ordinal));
            Assert.Contains(cluster, statement => statement.Contains($"\"{ApplicationRole}\"", StringComparison.Ordinal));
            Assert.Contains(cluster, statement =>
                statement.Contains($"CREATE DATABASE \"{DatabaseName}\" OWNER \"{OwnerRole}\"", StringComparison.Ordinal));

            var schemaRevoke = _resolver.TenantStatements.FindIndex(entry =>
                entry.Statement.StartsWith("REVOKE ALL ON SCHEMA", StringComparison.Ordinal));
            Assert.True(schemaRevoke >= 0);
            Assert.Equal(DatabaseName, _resolver.TenantStatements[schemaRevoke].Database);
            Assert.Contains(_resolver.TenantStatements, entry =>
                entry.Statement.Contains($"GRANT USAGE ON SCHEMA public TO \"{ApplicationRole}\"", StringComparison.Ordinal));
            Assert.Contains(_resolver.TenantStatements, entry =>
                entry.Statement.Contains($"ALTER DEFAULT PRIVILEGES FOR ROLE \"{OwnerRole}\"", StringComparison.Ordinal));

            Assert.DoesNotContain(cluster, statement => statement.Contains("DROP", StringComparison.Ordinal));
            Assert.DoesNotContain(_resolver.TenantStatements.Select(entry => entry.Statement),
                statement => statement.Contains("DROP", StringComparison.Ordinal));
        }

        [Fact]
        public async Task EnsureDatabaseAsync_ReportsTheDerivedNamesAndCredentialReference_WithoutAnyCredential()
        {
            var result = await CreateCreator().EnsureDatabaseAsync(Spec, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(DatabaseName, result.Value!.DatabaseName);
            Assert.Equal(OwnerRole, result.Value.OwnerRoleName);
            Assert.Equal(ApplicationRole, result.Value.ApplicationRoleName);
            Assert.Equal("v1", result.Value.CredentialReference);
        }

        [Fact]
        public async Task EnsureDatabaseAsync_WhenRunTwice_ReusesDatabaseAndResetsRolePassword_WithoutCreatingANewOne()
        {
            var creator = CreateCreator();

            await creator.EnsureDatabaseAsync(Spec, CancellationToken.None);
            var secondRunCluster = _resolver.ClusterStatements.Count;

            var secondResult = await creator.EnsureDatabaseAsync(Spec, CancellationToken.None);

            Assert.True(secondResult.IsSuccess);
            var secondRunStatements = _resolver.ClusterStatements.Skip(secondRunCluster).ToList();

            Assert.Contains(secondRunStatements, statement =>
                statement.StartsWith($"ALTER ROLE \"{OwnerRole}\" WITH LOGIN PASSWORD ", StringComparison.Ordinal));
            Assert.Contains(secondRunStatements, statement =>
                statement.StartsWith($"ALTER ROLE \"{ApplicationRole}\" WITH LOGIN PASSWORD ", StringComparison.Ordinal));
            Assert.DoesNotContain(secondRunStatements, statement =>
                statement.StartsWith("CREATE ROLE ", StringComparison.Ordinal));
            Assert.DoesNotContain(secondRunStatements, statement =>
                statement.StartsWith("CREATE DATABASE ", StringComparison.Ordinal));
            Assert.DoesNotContain(secondRunStatements, statement =>
                statement.Contains("DROP", StringComparison.Ordinal));
        }

        [Fact]
        public async Task EnsureDatabaseAsync_NeverLogsTheDerivedCredential()
        {
            var derived = _deriver.Derive(MasterKey, TenantId);

            var successResult = await CreateCreator().EnsureDatabaseAsync(Spec, CancellationToken.None);

            Assert.True(successResult.IsSuccess);
            Assert.Contains(_resolver.ClusterStatements, statement =>
                statement.Contains(SqlLiteral.Quote(derived), StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, message => message.Contains(derived, StringComparison.Ordinal));
        }

        [Fact]
        public async Task EnsureDatabaseAsync_WithAFailingExecutor_ReturnsASanitizedFailureAndLogsWithoutSecrets()
        {
            _resolver.FailureToThrow = new InvalidOperationException("connection failed: Host=db;Password=leak-secret");

            var result = await CreateCreator().EnsureDatabaseAsync(Spec, CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.EnsureDatabaseFailedCode, result.Error!.Code);
            Assert.DoesNotContain(_logger.Messages, message => message.Contains("Password=", StringComparison.Ordinal));
            Assert.DoesNotContain(_logger.Messages, message => message.Contains("leak-secret", StringComparison.Ordinal));
        }

        [Fact]
        public async Task EnsureDatabaseAsync_WithAnEmptyTenantId_ReturnsAValidationFailureAndExecutesNothing()
        {
            var result = await CreateCreator().EnsureDatabaseAsync(
                new TenantDatabaseSpec(Guid.Empty, "cool-cafe"),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.InvalidTenantIdCode, result.Error!.Code);
            Assert.Empty(_resolver.OrderedStatements);
        }

        [Fact]
        public async Task EnsureDatabaseAsync_WithAnInvalidSlug_ReturnsAValidationFailureAndExecutesNothing()
        {
            var result = await CreateCreator().EnsureDatabaseAsync(
                new TenantDatabaseSpec(TenantId, "Bad_Slug"),
                CancellationToken.None);

            Assert.True(result.IsFailure);
            Assert.Equal(ProvisioningErrors.InvalidTenantSlugCode, result.Error!.Code);
            Assert.Empty(_resolver.OrderedStatements);
        }

        private sealed class FakeResolver : ITenantProvisioningSqlExecutor
        {
            public HashSet<string> Roles { get; } = new(StringComparer.Ordinal);

            public HashSet<string> Databases { get; } = new(StringComparer.Ordinal);

            public List<string> ClusterStatements { get; } = [];

            public List<(string Database, string Statement)> TenantStatements { get; } = [];

            public List<string> OrderedStatements { get; } = [];

            public Exception? FailureToThrow { get; set; }

            public Task<bool> RoleExistsAsync(string roleName, CancellationToken cancellationToken)
            {
                return Task.FromResult(Roles.Contains(roleName));
            }

            public Task<bool> DatabaseExistsAsync(string databaseName, CancellationToken cancellationToken)
            {
                return Task.FromResult(Databases.Contains(databaseName));
            }

            public Task ExecuteClusterStatementAsync(string statement, CancellationToken cancellationToken)
            {
                OrderedStatements.Add("CLUSTER:" + statement);
                ClusterStatements.Add(statement);
                ApplyClusterEffects(statement);
                if (FailureToThrow is not null)
                {
                    throw FailureToThrow;
                }

                return Task.CompletedTask;
            }

            public Task ExecuteTenantStatementAsync(string databaseName, string statement, CancellationToken cancellationToken)
            {
                OrderedStatements.Add("TENANT:" + statement);
                TenantStatements.Add((databaseName, statement));
                if (FailureToThrow is not null)
                {
                    throw FailureToThrow;
                }

                return Task.CompletedTask;
            }

            private void ApplyClusterEffects(string statement)
            {
                if (statement.StartsWith("CREATE ROLE ", StringComparison.Ordinal))
                {
                    Roles.Add(FirstQuotedToken(statement));
                }
                else if (statement.StartsWith("CREATE DATABASE ", StringComparison.Ordinal))
                {
                    Databases.Add(FirstQuotedToken(statement));
                }
            }

            private static string FirstQuotedToken(string statement)
            {
                var first = statement.IndexOf('"');
                var second = statement.IndexOf('"', first + 1);
                return statement.Substring(first + 1, second - first - 1);
            }
        }

        private sealed class CapturingLogger : ILogger<TenantDatabaseCreator>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                Messages.Add(formatter(state, exception)!);
            }
        }
    }
}
