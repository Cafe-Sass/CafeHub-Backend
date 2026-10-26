using CafeHub.Application.Abstractions.Persistence;
using CafeHub.Application.Tenancy;
using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Tenant;
using CafeHub.Infrastructure.Provisioning;
using CafeHub.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CafeHub.UnitTests
{
    public sealed class TenantInfrastructureRegistrationTests
    {
        private static ServiceProvider BuildProvider(
            Dictionary<string, string?> settings,
            Action<ITenantContextSetter>? prepareTenant = null,
            TenantRegistration? tenantRegistration = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);
            services.Replace(ServiceDescriptor.Scoped<ITenantRegistryReader>(
                _ => new TenantRegistryReaderStub(tenantRegistration)));

            var serviceProvider = services.BuildServiceProvider();

            prepareTenant?.Invoke(serviceProvider.GetRequiredService<ITenantContextSetter>());

            return serviceProvider;
        }

        [Fact]
        public void ResolvingTenantDbContext_Throws_WhenNoTenantContextIsSet()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>());

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<TenantDbContext>());

            Assert.IsType<TenantContextMissingException>(exception);
        }

        [Fact]
        public void ResolvingTenantDbContext_Throws_WhenTenantIsInactive()
        {
            using var serviceProvider = BuildProvider(
                new Dictionary<string, string?> { ["TenantConnections:cafe-alpha"] = "Host=localhost;Port=5432;Database=cafehub_tenant_alpha" },
                setter => setter.Set(TenantContext.Create(Guid.NewGuid(), "cafe-alpha", false)));

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<TenantDbContext>());

            Assert.IsType<TenantInactiveException>(exception);
        }

        [Fact]
        public void ResolvingTenantUnitOfWork_Throws_WhenNoTenantContextIsSet()
        {
            using var serviceProvider = BuildProvider(new Dictionary<string, string?>());

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<ITenantUnitOfWork>());

            Assert.IsType<TenantContextMissingException>(exception);
        }

        [Fact]
        public void ResolvingTenantUnitOfWork_Throws_WhenTenantIsInactive()
        {
            using var serviceProvider = BuildProvider(
                new Dictionary<string, string?> { ["TenantConnections:cafe-alpha"] = "Host=localhost;Port=5432;Database=cafehub_tenant_alpha" },
                setter => setter.Set(TenantContext.Create(Guid.NewGuid(), "cafe-alpha", false)));

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<ITenantUnitOfWork>());

            Assert.IsType<TenantInactiveException>(exception);
        }

        [Fact]
        public void ResolvingTenantDbContext_UsesTenantConnection_and_DoesNotOpenConnection()
        {
            var tenantId = Guid.NewGuid();
            var settings = CreateTenantConnectionSettings();
            using var serviceProvider = BuildProvider(
                settings,
                setter => setter.Set(TenantContext.Create(tenantId, "ignored-context-reference", true)),
                new TenantRegistration(tenantId, "cafe-alpha", TenantLifecycleStatus.Active, "v1"));

            var tenantDbContext = serviceProvider.GetRequiredService<TenantDbContext>();
            var builder = new NpgsqlConnectionStringBuilder(tenantDbContext.Database.GetConnectionString());

            Assert.Equal("localhost", builder.Host);
            Assert.Equal(5544, builder.Port);
            Assert.Equal("cafehub_tenant_cafe_alpha", builder.Database);
            Assert.Equal("cafehub_tenant_cafe_alpha_app", builder.Username);
            Assert.Equal(SslMode.Require, builder.SslMode);
            Assert.True(builder.Pooling);
            Assert.Equal(23, builder.MaxPoolSize);
            Assert.Equal("cafehub-tenant", builder.ApplicationName);
            var expectedPassword = new TenantCredentialDeriver().Derive(GetCredentialMasterKey(), tenantId);
            Assert.True(
                string.Equals(expectedPassword, builder.Password, StringComparison.Ordinal),
                "the connection should use the shared derived tenant credential");
        }

        [Fact]
        public void ResolvingTenantDbContext_NeverFallsBackToMasterConnection()
        {
            const string masterConnection = "Host=localhost;Port=5432;Database=cafehub_master";
            using var serviceProvider = BuildProvider(
                new Dictionary<string, string?> { [ConfigurationKeys.MasterConnectionString] = masterConnection },
                setter => setter.Set(TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true)),
                tenantRegistration: null);

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<TenantDbContext>());

            Assert.IsType<InvalidTenantContextException>(exception);
            Assert.DoesNotContain(masterConnection, exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("cafehub_master", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ResolvingMasterDbContext_NeverTouchesTenantSettings()
        {
            const string masterConnection = "Host=localhost;Port=5432;Database=cafehub_master";
            using var serviceProvider = BuildProvider(new Dictionary<string, string?> { [ConfigurationKeys.MasterConnectionString] = masterConnection });

            var masterDbContext = serviceProvider.GetRequiredService<MasterDbContext>();

            Assert.Equal(masterConnection, masterDbContext.Database.GetConnectionString());
        }

        private static Dictionary<string, string?> CreateTenantConnectionSettings()
        {
            return new Dictionary<string, string?>
            {
                ["TenantDatabases:Host"] = "localhost",
                ["TenantDatabases:Port"] = "5544",
                ["TenantDatabases:SslMode"] = "Require",
                ["TenantDatabases:MaxPoolSize"] = "23",
                ["Provisioning:AdminConnectionString"] = "Host=localhost;Database=provisioning",
                ["Provisioning:CredentialMasterKey"] = Convert.ToBase64String(GetCredentialMasterKey()),
            };
        }

        private static byte[] GetCredentialMasterKey()
        {
            return Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        }

        private sealed class TenantRegistryReaderStub(TenantRegistration? registration) : ITenantRegistryReader
        {
            public Task<TenantRegistration?> FindBySlugAsync(string normalizedSlug, CancellationToken cancellationToken)
            {
                return Task.FromResult<TenantRegistration?>(null);
            }

            public Task<TenantRegistration?> FindByIdAsync(Guid tenantId, CancellationToken cancellationToken)
            {
                return Task.FromResult(registration?.TenantId == tenantId ? registration : null);
            }
        }
    }
}
