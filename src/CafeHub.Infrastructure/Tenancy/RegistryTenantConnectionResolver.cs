using CafeHub.Application.Provisioning;
using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure.Provisioning;
using Npgsql;

namespace CafeHub.Infrastructure.Tenancy
{
    internal sealed class RegistryTenantConnectionResolver(
        ITenantContextAccessor tenantContextAccessor,
        ITenantRegistryReader tenantRegistryReader,
        TenantDatabaseOptions databaseOptions,
        TenantDatabaseCreatorOptions creatorOptions,
        ITenantCredentialDeriver credentialDeriver) : ITenantConnectionResolver
    {
        public string Resolve()
        {
            var tenantContext = tenantContextAccessor.GetRequired();
            var tenant = tenantRegistryReader
                .FindByIdAsync(tenantContext.TenantId, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (tenant is null
                || string.IsNullOrWhiteSpace(tenant.SecretReference)
                || !TenantDatabaseNaming.IsValidTenantSlug(tenant.Slug))
            {
                throw new InvalidTenantContextException("The tenant context does not resolve to a usable tenant registration.");
            }

            if (!string.Equals(
                tenant.SecretReference,
                TenantDatabaseCreator.CredentialReferenceLabel,
                StringComparison.Ordinal))
            {
                throw new TenantConnectionNotConfiguredException("The tenant credential reference is not supported.");
            }

            var databaseName = TenantDatabaseNaming.DatabaseName(tenant.Slug);
            var roleName = TenantDatabaseNaming.ApplicationRoleName(databaseName);

            try
            {
                var password = credentialDeriver.Derive(
                    Convert.FromBase64String(creatorOptions.CredentialMasterKey),
                    tenant.TenantId);
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = databaseOptions.Host,
                    Port = databaseOptions.Port,
                    Database = databaseName,
                    Username = roleName,
                    Password = password,
                    SslMode = databaseOptions.SslMode,
                    Pooling = true,
                    MaxPoolSize = databaseOptions.MaxPoolSize,
                    ApplicationName = "cafehub-tenant",
                };

                return builder.ConnectionString;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                throw new TenantConnectionNotConfiguredException("The tenant connection could not be resolved.");
            }
        }
    }
}
