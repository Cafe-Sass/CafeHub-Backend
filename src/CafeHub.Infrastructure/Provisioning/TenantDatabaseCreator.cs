using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Provisioning;
using CafeHub.Application.Provisioning.Contracts;
using Microsoft.Extensions.Logging;

namespace CafeHub.Infrastructure.Provisioning
{
    /// <summary>
    /// Creates the per-tenant PostgreSQL database and its dedicated role pair idempotently, executing
    /// exactly the create-role, create-database, and revoke/grant statements the reviewed PostgreSQL
    /// operations commands define. The derived role credential is passed only to the password statements
    /// and is never logged or returned.
    /// </summary>
    internal sealed class TenantDatabaseCreator : ITenantDatabaseCreator
    {
        internal const string CredentialReferenceLabel = "v1";

        private readonly TenantDatabaseCreatorOptions _options;
        private readonly ITenantCredentialDeriver _credentialDeriver;
        private readonly ITenantProvisioningSqlExecutor _executor;
        private readonly ILogger<TenantDatabaseCreator> _logger;

        public TenantDatabaseCreator(
            TenantDatabaseCreatorOptions options,
            ITenantCredentialDeriver credentialDeriver,
            ITenantProvisioningSqlExecutor executor,
            ILogger<TenantDatabaseCreator> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _credentialDeriver = credentialDeriver ?? throw new ArgumentNullException(nameof(credentialDeriver));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<Result<TenantDatabaseInfo>> EnsureDatabaseAsync(
            TenantDatabaseSpec spec,
            CancellationToken cancellationToken)
        {
            if (spec is null || spec.TenantId == Guid.Empty)
            {
                return Result.Failure<TenantDatabaseInfo>(ProvisioningErrors.InvalidTenantId());
            }

            if (!TenantDatabaseNaming.IsValidTenantSlug(spec.TenantSlug))
            {
                return Result.Failure<TenantDatabaseInfo>(ProvisioningErrors.InvalidTenantSlug());
            }

            var databaseName = TenantDatabaseNaming.DatabaseName(spec.TenantSlug);
            var ownerRoleName = TenantDatabaseNaming.OwnerRoleName(databaseName);
            var applicationRoleName = TenantDatabaseNaming.ApplicationRoleName(databaseName);

            if (!TenantDatabaseNaming.IsValidDatabaseName(databaseName)
                || !SqlIdentifier.IsValid(ownerRoleName)
                || !SqlIdentifier.IsValid(applicationRoleName))
            {
                return Result.Failure<TenantDatabaseInfo>(ProvisioningErrors.InvalidTenantSlug());
            }

            try
            {
                var rolePassword = _credentialDeriver.Derive(
                    Convert.FromBase64String(_options.CredentialMasterKey),
                    spec.TenantId);

                await CreateOrUpdateRoleAsync(ownerRoleName, rolePassword, cancellationToken);
                await CreateOrUpdateRoleAsync(applicationRoleName, rolePassword, cancellationToken);

                if (!await _executor.DatabaseExistsAsync(databaseName, cancellationToken))
                {
                    await _executor.ExecuteClusterStatementAsync(
                        $"CREATE DATABASE {SqlIdentifier.Quote(databaseName)} OWNER {SqlIdentifier.Quote(ownerRoleName)}",
                        cancellationToken);
                }

                await _executor.ExecuteClusterStatementAsync(
                    $"REVOKE ALL ON DATABASE {SqlIdentifier.Quote(databaseName)} FROM PUBLIC",
                    cancellationToken);
                await _executor.ExecuteClusterStatementAsync(
                    $"GRANT CONNECT ON DATABASE {SqlIdentifier.Quote(databaseName)} TO {SqlIdentifier.Quote(ownerRoleName)}, {SqlIdentifier.Quote(applicationRoleName)}",
                    cancellationToken);

                await _executor.ExecuteTenantStatementAsync(
                    databaseName,
                    "REVOKE ALL ON SCHEMA public FROM PUBLIC",
                    cancellationToken);
                await _executor.ExecuteTenantStatementAsync(
                    databaseName,
                    $"GRANT USAGE ON SCHEMA public TO {SqlIdentifier.Quote(applicationRoleName)}",
                    cancellationToken);
                await _executor.ExecuteTenantStatementAsync(
                    databaseName,
                    $"ALTER DEFAULT PRIVILEGES FOR ROLE {SqlIdentifier.Quote(ownerRoleName)} IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {SqlIdentifier.Quote(applicationRoleName)}",
                    cancellationToken);
                await _executor.ExecuteTenantStatementAsync(
                    databaseName,
                    $"ALTER DEFAULT PRIVILEGES FOR ROLE {SqlIdentifier.Quote(ownerRoleName)} IN SCHEMA public GRANT USAGE ON SEQUENCES TO {SqlIdentifier.Quote(applicationRoleName)}",
                    cancellationToken);

                return Result.Success(
                    new TenantDatabaseInfo(databaseName, ownerRoleName, applicationRoleName, CredentialReferenceLabel));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    "Tenant database provisioning failed for {DatabaseName} ({ExceptionType}).",
                    databaseName,
                    exception.GetType().Name);
                return Result.Failure<TenantDatabaseInfo>(ProvisioningErrors.EnsureDatabaseFailed());
            }
        }

        private async Task CreateOrUpdateRoleAsync(string roleName, string rolePassword, CancellationToken cancellationToken)
        {
            var exists = await _executor.RoleExistsAsync(roleName, cancellationToken);
            var statement = exists
                ? $"ALTER ROLE {SqlIdentifier.Quote(roleName)} WITH LOGIN PASSWORD {SqlLiteral.Quote(rolePassword)}"
                : $"CREATE ROLE {SqlIdentifier.Quote(roleName)} LOGIN PASSWORD {SqlLiteral.Quote(rolePassword)}";

            await _executor.ExecuteClusterStatementAsync(statement, cancellationToken);
        }
    }
}
