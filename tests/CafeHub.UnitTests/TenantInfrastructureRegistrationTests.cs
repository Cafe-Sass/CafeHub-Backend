using CafeHub.Application.Tenancy;
using CafeHub.Infrastructure;
using CafeHub.Infrastructure.Persistence.Master;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CafeHub.UnitTests
{
    public sealed class TenantInfrastructureRegistrationTests
    {
        private static ServiceProvider BuildProvider(Dictionary<string, string?> settings, Action<ITenantContextSetter>? prepareTenant = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddInfrastructure(configuration);

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
        public void ResolvingTenantDbContext_UsesTenantConnection_and_DoesNotOpenConnection()
        {
            const string configuredConnection = "Host=localhost;Port=5432;Database=cafehub_tenant_alpha";
            using var serviceProvider = BuildProvider(
                new Dictionary<string, string?> { ["TenantConnections:cafe-alpha"] = configuredConnection },
                setter => setter.Set(TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true)));

            var tenantDbContext = serviceProvider.GetRequiredService<TenantDbContext>();

            Assert.Equal(configuredConnection, tenantDbContext.Database.GetConnectionString());
        }

        [Fact]
        public void ResolvingTenantDbContext_NeverFallsBackToMasterConnection()
        {
            const string masterConnection = "Host=localhost;Port=5432;Database=cafehub_master";
            using var serviceProvider = BuildProvider(
                new Dictionary<string, string?> { [ConfigurationKeys.MasterConnectionString] = masterConnection },
                setter => setter.Set(TenantContext.Create(Guid.NewGuid(), "cafe-alpha", true)));

            var exception = Record.Exception(() => serviceProvider.GetRequiredService<TenantDbContext>());

            Assert.IsType<TenantConnectionNotConfiguredException>(exception);
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
    }
}
