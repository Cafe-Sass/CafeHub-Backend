using CafeHub.Application.Provisioning;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Domain.Tenants.Credentials;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Tenancy;
using Npgsql;

namespace CafeHub.UnitTests.Tenancy.Infrastructure
{
    public sealed class RegistryTenantConnectionResolverTests
    {
        private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();

        [Fact]
        public void Resolve_BuildsConnectionFromRegistryAndValidatedOptions()
        {
            var tenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var registry = new TenantRegistryReaderStub(new TenantRegistration(
                tenantId,
                "cafe-alpha",
                TenantLifecycleStatus.Active,
                TenantDatabaseCredentialReference.CreateV1(tenantId)));
            var accessor = new TenantContextAccessor();
            accessor.Set(TenantContext.Create(tenantId, "caller-value-is-ignored", true));
            var resolver = CreateResolver(accessor, registry, CreateOptions());

            var builder = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            Assert.Equal("db.example.test", builder.Host);
            Assert.Equal(5441, builder.Port);
            Assert.Equal("cafehub_tenant_cafe_alpha", builder.Database);
            Assert.Equal("cafehub_tenant_cafe_alpha_app", builder.Username);
            Assert.Equal(SslMode.VerifyFull, builder.SslMode);
            Assert.True(builder.Pooling);
            Assert.Equal(17, builder.MaxPoolSize);
            Assert.Equal("cafehub-tenant", builder.ApplicationName);
            var expectedPassword = new HmacTenantCredentialDeriver().Derive(MasterKey, tenantId);
            Assert.True(
                string.Equals(expectedPassword, builder.Password, StringComparison.Ordinal),
                "the builder should use the registry tenant's derived credential");
            Assert.Equal(1, registry.IdLookupCount);
        }

        [Fact]
        public void Resolve_UsesDistinctConnectionValuesForSequentialTenants()
        {
            var firstId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var secondId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var registry = new TenantRegistryReaderStub(
                new TenantRegistration(firstId, "cafe-alpha", TenantLifecycleStatus.Active, TenantDatabaseCredentialReference.CreateV1(firstId)),
                new TenantRegistration(secondId, "cafe-beta", TenantLifecycleStatus.Active, TenantDatabaseCredentialReference.CreateV1(secondId)));
            var accessor = new TenantContextAccessor();
            var resolver = CreateResolver(accessor, registry, CreateOptions());

            accessor.Set(TenantContext.Create(firstId, TenantDatabaseCredentialReference.CreateV1(firstId), true));
            var first = new NpgsqlConnectionStringBuilder(resolver.Resolve());
            var firstRepeated = new NpgsqlConnectionStringBuilder(resolver.Resolve());
            accessor.Set(TenantContext.Create(secondId, TenantDatabaseCredentialReference.CreateV1(secondId), true));
            var second = new NpgsqlConnectionStringBuilder(resolver.Resolve());

            Assert.NotEqual(first.Database, second.Database);
            Assert.NotEqual(first.Username, second.Username);
            Assert.True(
                !string.Equals(first.Password, second.Password, StringComparison.Ordinal),
                "different tenants must have different derived passwords");
            Assert.True(
                string.Equals(first.ConnectionString, firstRepeated.ConnectionString, StringComparison.Ordinal),
                "repeated resolution for one tenant should be deterministic");
            Assert.Equal(3, registry.IdLookupCount);
        }

        [Fact]
        public void Resolve_ContinuesToSupportLegacyV1Registrations()
        {
            var tenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var registry = new TenantRegistryReaderStub(new TenantRegistration(
                tenantId,
                "cafe-alpha",
                TenantLifecycleStatus.Active,
                TenantDatabaseCredentialReference.LegacyV1));
            var accessor = new TenantContextAccessor();
            accessor.Set(TenantContext.Create(tenantId, TenantDatabaseCredentialReference.LegacyV1, true));

            var builder = new NpgsqlConnectionStringBuilder(CreateResolver(accessor, registry, CreateOptions()).Resolve());

            Assert.Equal("cafehub_tenant_cafe_alpha", builder.Database);
            Assert.Equal(new HmacTenantCredentialDeriver().Derive(MasterKey, tenantId), builder.Password);
        }

        [Fact]
        public void Resolve_RejectsCredentialReferenceScopedToAnotherTenant()
        {
            var tenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var otherTenantId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
            var accessor = new TenantContextAccessor();
            accessor.Set(TenantContext.Create(tenantId, "ignored", true));
            var registry = new TenantRegistryReaderStub(new TenantRegistration(
                tenantId,
                "cafe-alpha",
                TenantLifecycleStatus.Active,
                TenantDatabaseCredentialReference.CreateV1(otherTenantId)));
            var resolver = CreateResolver(accessor, registry, CreateOptions());

            Assert.Throws<TenantConnectionNotConfiguredException>(() => resolver.Resolve());
        }

        [Fact]
        public void Resolve_WhenContextIsMissing_FailsBeforeRegistryLookup()
        {
            var registry = new TenantRegistryReaderStub();
            var resolver = CreateResolver(new TenantContextAccessor(), registry, CreateOptions());

            var exception = Assert.Throws<TenantContextMissingException>(() => resolver.Resolve());

            Assert.DoesNotContain("Password=", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, registry.IdLookupCount);
        }

        [Fact]
        public void Resolve_WhenTenantIsUnknown_FailsClosed()
        {
            var tenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var accessor = new TenantContextAccessor();
            accessor.Set(TenantContext.Create(tenantId, "v1", true));
            var resolver = CreateResolver(accessor, new TenantRegistryReaderStub(), CreateOptions());

            var exception = Assert.Throws<InvalidTenantContextException>(() => resolver.Resolve());

            Assert.DoesNotContain("Password=", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Resolve_WhenCredentialReferenceIsUnknown_FailsClosedWithoutExposingCredentialMaterial()
        {
            var tenantId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
            var accessor = new TenantContextAccessor();
            accessor.Set(TenantContext.Create(tenantId, "caller-value-is-ignored", true));
            var registry = new TenantRegistryReaderStub(new TenantRegistration(
                tenantId,
                "cafe-alpha",
                TenantLifecycleStatus.Active,
                "v2"));
            var resolver = CreateResolver(accessor, registry, CreateOptions());

            var exception = Assert.Throws<TenantConnectionNotConfiguredException>(() => resolver.Resolve());

            Assert.DoesNotContain(Convert.ToBase64String(MasterKey), exception.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Password=", exception.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static RegistryTenantConnectionResolver CreateResolver(
            ITenantContextAccessor accessor,
            ITenantRegistryReader registry,
            TenantDatabaseOptions options)
        {
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

        private static TenantDatabaseOptions CreateOptions()
        {
            return new TenantDatabaseOptions
            {
                Host = "db.example.test",
                Port = 5441,
                SslMode = SslMode.VerifyFull,
                MaxPoolSize = 17,
            };
        }

        private sealed class TenantRegistryReaderStub(params TenantRegistration[] registrations) : ITenantRegistryReader
        {
            public int IdLookupCount { get; private set; }

            public Task<TenantRegistration?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken)
            {
                return Task.FromResult(registrations.FirstOrDefault(
                    registration => string.Equals(registration.Slug, normalizedSlug, StringComparison.OrdinalIgnoreCase)));
            }

            public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                IdLookupCount++;
                return Task.FromResult(registrations.FirstOrDefault(registration => registration.TenantId == tenantId));
            }
        }
    }
}
