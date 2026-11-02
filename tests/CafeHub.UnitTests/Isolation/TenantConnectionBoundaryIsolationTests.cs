using CafeHub.Application.Provisioning;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Domain.Tenants.Credentials;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Tenancy;
using Npgsql;

namespace CafeHub.UnitTests.Isolation
{
    /// <summary>
    /// Proves tenant isolation at the connection resolution boundary.
    /// Note: The data-access boundary (a real database per tenant) is NOT proven by automated unit tests;
    /// it is covered by documented manual verification.
    /// </summary>
    public sealed class TenantConnectionBoundaryIsolationTests
    {
        private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(val => (byte)val).ToArray();
        private static readonly Guid TenantAId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        private static readonly Guid TenantBId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

        [Fact]
        public void ConnectionResolver_YieldsDifferentDatabaseRoleAndPasswordForTenantsAAndB()
        {
            var registry = new FakeTenantRegistryReader(
                new TenantRegistration(TenantAId, "cafe-a", TenantLifecycleStatus.Active, TenantDatabaseCredentialReference.CreateV1(TenantAId)),
                new TenantRegistration(TenantBId, "cafe-b", TenantLifecycleStatus.Active, TenantDatabaseCredentialReference.CreateV1(TenantBId)));

            var accessor = new TenantContextAccessor();
            var resolver = CreateResolver(accessor, registry);

            accessor.Set(TenantContext.Create(TenantAId, TenantDatabaseCredentialReference.CreateV1(TenantAId), true, "cafe-a"));
            var connectionA = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            accessor.Set(TenantContext.Create(TenantBId, TenantDatabaseCredentialReference.CreateV1(TenantBId), true, "cafe-b"));
            var connectionB = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            Assert.NotEqual(connectionA.Database, connectionB.Database);
            Assert.NotEqual(connectionA.Username, connectionB.Username);
            Assert.False(string.Equals(connectionA.Password, connectionB.Password, StringComparison.Ordinal));
        }

        [Fact]
        public void ConnectionResolver_NeverReturnsTenantsAValuesWhileTenantBContextIsActive()
        {
            var registry = new FakeTenantRegistryReader(
                new TenantRegistration(TenantAId, "cafe-a", TenantLifecycleStatus.Active, TenantDatabaseCredentialReference.CreateV1(TenantAId)),
                new TenantRegistration(TenantBId, "cafe-b", TenantLifecycleStatus.Active, TenantDatabaseCredentialReference.CreateV1(TenantBId)));

            var accessor = new TenantContextAccessor();
            var resolver = CreateResolver(accessor, registry);

            accessor.Set(TenantContext.Create(TenantAId, TenantDatabaseCredentialReference.CreateV1(TenantAId), true, "cafe-a"));
            var connectionA = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            accessor.Set(TenantContext.Create(TenantBId, TenantDatabaseCredentialReference.CreateV1(TenantBId), true, "cafe-b"));
            var connectionB = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            Assert.DoesNotContain("cafe_a", connectionB.Database, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("cafe_a", connectionB.Username, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(connectionA.Password, connectionB.Password);

            Assert.Equal("cafehub_tenant_cafe_b", connectionB.Database);
            Assert.Equal("cafehub_tenant_cafe_b_app", connectionB.Username);
        }

        private static RegistryTenantConnectionResolver CreateResolver(
            ITenantContextAccessor accessor,
            ITenantRegistryReader registry)
        {
            var options = new TenantDatabaseOptions
            {
                Host = "tenant-db.example.internal",
                Port = 5432,
                SslMode = SslMode.Require,
                MaxPoolSize = 25,
            };

            var creatorOptions = new TenantDatabaseCreatorOptions
            {
                CredentialMasterKey = Convert.ToBase64String(MasterKey),
            };

            return new RegistryTenantConnectionResolver(
                accessor,
                registry,
                new Lazy<TenantDatabaseOptions>(() => options),
                new Lazy<TenantDatabaseCreatorOptions>(() => creatorOptions),
                new HmacTenantCredentialDeriver(),
                new TenantStateGuard());
        }

        private sealed class FakeTenantRegistryReader(params TenantRegistration[] registrations) : ITenantRegistryReader
        {
            private readonly Dictionary<Guid, TenantRegistration> byId = registrations.ToDictionary(r => r.TenantId);

            public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken) =>
                Task.FromResult(byId.GetValueOrDefault(tenantId));

            public Task<TenantRegistration?> FindBySlugAsync(string slug, CancellationToken cancellationToken) =>
                Task.FromResult(byId.Values.FirstOrDefault(r => string.Equals(r.Slug, slug, StringComparison.OrdinalIgnoreCase)));
        }
    }
}
