using CafeHub.Application.Provisioning;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Domain.Tenants.Credentials;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Tenancy;
using Npgsql;

namespace CafeHub.UnitTests.Tenancy.Infrastructure
{
    public sealed class TenantDatabaseRoleTests
    {
        [Fact]
        public void Resolve_UsesDerivedApplicationRoleAndTenantCredentialReference()
        {
            var tenantId = Guid.CreateVersion7();
            var masterKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            var deriver = new TenantCredentialDeriver();
            var registry = new StubRegistry(new TenantRegistration(
                tenantId,
                "demo-cafe-271027e",
                TenantLifecycleStatus.Active,
                TenantDatabaseCredentialReference.LegacyV1));
            var accessor = new StubTenantContextAccessor(TenantContext.Create(
                tenantId,
                TenantDatabaseCredentialReference.LegacyV1,
                true,
                "demo-cafe-271027e"));
            var resolver = new RegistryTenantConnectionResolver(
                accessor,
                registry,
                new Lazy<TenantDatabaseOptions>(() => new TenantDatabaseOptions
                {
                    Host = "db.example.test",
                    Port = 5432,
                    SslMode = SslMode.Require,
                    MaxPoolSize = 10,
                }),
                new Lazy<TenantDatabaseCreatorOptions>(() => new TenantDatabaseCreatorOptions
                {
                    CredentialMasterKey = Convert.ToBase64String(masterKey),
                }),
                deriver,
                new TenantStateGuard());

            var connectionString = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            Assert.Equal("cafehub_tenant_demo_cafe_271027e", connectionString.Database);
            Assert.Equal("cafehub_tenant_demo_cafe_271027e_app", connectionString.Username);
            Assert.Equal(
                deriver.Derive(masterKey, tenantId),
                connectionString.Password);
        }

        private sealed class StubRegistry(TenantRegistration registration) : ITenantRegistryReader
        {
            public Task<TenantRegistration?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken) =>
                Task.FromResult<TenantRegistration?>(registration.Slug == normalizedSlug ? registration : null);

            public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
                Task.FromResult<TenantRegistration?>(registration.TenantId == tenantId ? registration : null);
        }

        private sealed class StubTenantContextAccessor(TenantContext context) : ITenantContextAccessor
        {
            public TenantContext GetRequired() => context;
        }
    }
}
