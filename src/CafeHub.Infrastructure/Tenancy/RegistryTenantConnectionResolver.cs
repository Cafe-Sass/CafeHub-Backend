using CafeHub.Application.Provisioning;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants.Credentials;
using CafeHub.Infrastructure.Provisioning;
using Npgsql;

namespace CafeHub.Infrastructure.Tenancy
{
    internal sealed class RegistryTenantConnectionResolver(
        ITenantContextAccessor tenantContextAccessor,
        ITenantRegistryReader tenantRegistryReader,
        Lazy<TenantDatabaseOptions> databaseOptions,
        Lazy<TenantDatabaseCreatorOptions> creatorOptions,
        ITenantCredentialDeriver credentialDeriver,
        ITenantStateGuard tenantStateGuard) : ITenantConnectionResolver
    {
        public string Resolve()
        {
            var tenantContext = tenantContextAccessor.GetRequired();
            if (!tenantContext.IsActive)
            {
                throw new TenantInactiveException($"Tenant {tenantContext.TenantId} is not active. Activate the tenant before resolving its connection.");
            }

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

            if (tenantStateGuard.EnsureUsable(new ResolvedTenant(
                tenant.TenantId,
                tenant.Slug,
                tenant.State,
                tenant.SecretReference)).IsFailure)
            {
                throw new InvalidTenantContextException("The tenant context does not resolve to a usable tenant registration.");
            }

            if (!TenantDatabaseCredentialReference.IsSupportedForTenant(tenant.SecretReference, tenant.TenantId))
            {
                throw new TenantConnectionNotConfiguredException("The tenant credential reference is not supported.");
            }

            var databaseName = TenantDatabaseNaming.DatabaseName(tenant.Slug);
            var roleName = TenantDatabaseNaming.ApplicationRoleName(databaseName);

            try
            {
                var password = credentialDeriver.Derive(
                    Convert.FromBase64String(creatorOptions.Value.CredentialMasterKey),
                    tenant.TenantId);
                var options = databaseOptions.Value;
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = options.Host,
                    Port = options.Port,
                    Database = databaseName,
                    Username = roleName,
                    Password = password,
                    SslMode = options.SslMode,
                    Pooling = true,
                    MaxPoolSize = options.MaxPoolSize,
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
