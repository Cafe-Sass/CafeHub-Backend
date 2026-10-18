using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Tenancy;
using Microsoft.Extensions.Configuration;

namespace CafeHub.UnitTests
{
    public sealed class TenantConnectionResolverTests
    {
        private static TenantConnectionResolver CreateResolver(Dictionary<string, string?>? settings = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings ?? new Dictionary<string, string?>())
                .Build();

            return new TenantConnectionResolver(new ConfigurationSecretReferenceResolver(configuration));
        }

        [Fact]
        public void Resolve_Throws_WhenTenantIsNull()
        {
            var resolver = CreateResolver();

            var exception = Record.Exception(() => resolver.Resolve(null!));

            Assert.IsType<InvalidTenantContextException>(exception);
        }

        [Fact]
        public void Resolve_Throws_WhenTenantIsInactive()
        {
            var resolver = CreateResolver();
            var tenantContext = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", false);

            var exception = Record.Exception(() => resolver.Resolve(tenantContext));

            Assert.IsType<TenantInactiveException>(exception);
            Assert.Contains(tenantContext.TenantId.ToString(), exception.Message);
            Assert.DoesNotContain("Host=", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_Throws_WhenTenantConnectionIsNotConfigured()
        {
            var resolver = CreateResolver();
            var tenantContext = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true);

            var exception = Record.Exception(() => resolver.Resolve(tenantContext));

            Assert.IsType<TenantConnectionNotConfiguredException>(exception);
            Assert.Contains($"{ConfigurationKeys.TenantConnectionsPrefix}cafe-alpha", exception.Message);
        }

        [Fact]
        public void Resolve_ReturnsConfiguredConnection_WhenTenantIsActive()
        {
            const string configuredConnection = "Host=localhost;Port=5432;Database=cafehub_tenant_alpha";
            var resolver = CreateResolver(new Dictionary<string, string?> { ["TenantConnections:cafe-alpha"] = configuredConnection });
            var tenantContext = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true);

            var connectionString = resolver.Resolve(tenantContext);

            Assert.Equal(configuredConnection, connectionString);
        }

        [Fact]
        public void Resolve_IsolatesTenants_WithDifferentSecretReferences()
        {
            var resolver = CreateResolver(new Dictionary<string, string?>
            {
                ["TenantConnections:cafe-alpha"] = "Host=localhost;Port=5432;Database=cafehub_tenant_alpha",
                ["TenantConnections:cafe-beta"] = "Host=localhost;Port=5432;Database=cafehub_tenant_beta"
            });
            var tenantAlpha = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true);
            var tenantBeta = TenantContext.Create(Guid.NewGuid(), "cafe-beta", true);

            Assert.Equal("Host=localhost;Port=5432;Database=cafehub_tenant_alpha", resolver.Resolve(tenantAlpha));
            Assert.Equal("Host=localhost;Port=5432;Database=cafehub_tenant_beta", resolver.Resolve(tenantBeta));
        }

        [Fact]
        public void Resolve_NeverFallsBackToMasterConnection()
        {
            const string masterConnection = "Host=localhost;Port=5432;Database=cafehub_master";
            var resolver = CreateResolver(new Dictionary<string, string?> { [ConfigurationKeys.MasterConnectionString] = masterConnection });
            var tenantContext = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true);

            var exception = Record.Exception(() => resolver.Resolve(tenantContext));

            Assert.IsType<TenantConnectionNotConfiguredException>(exception);
            Assert.DoesNotContain(masterConnection, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("cafehub_master", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Resolve_ExceptionMessages_NeverContainConnectionStrings()
        {
            const string connectionValue = "Host=secret-host;Password=secret-value";
            var resolver = CreateResolver(new Dictionary<string, string?> { ["TenantConnections:cafe-alpha"] = connectionValue });

            var activeTenant = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true);
            var configuredConnection = resolver.Resolve(activeTenant);

            Assert.Equal(connectionValue, configuredConnection);

            var inactiveTenant = TenantContext.Create(Guid.NewGuid(), "cafe-alpha", false);
            var inactiveException = Assert.Throws<TenantInactiveException>(() => resolver.Resolve(inactiveTenant));
            Assert.DoesNotContain(connectionValue, inactiveException.Message, StringComparison.Ordinal);

            var missingReferenceTenant = TenantContext.Create(Guid.NewGuid(), "cafe-missing", true);
            var missingException = Assert.Throws<TenantConnectionNotConfiguredException>(() => resolver.Resolve(missingReferenceTenant));
            Assert.DoesNotContain(connectionValue, missingException.Message, StringComparison.Ordinal);
        }
    }
}
