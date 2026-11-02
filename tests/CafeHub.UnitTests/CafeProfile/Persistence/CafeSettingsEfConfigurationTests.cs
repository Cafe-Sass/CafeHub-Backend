using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CafeHub.UnitTests.CafeProfile.Persistence
{
    public sealed class CafeSettingsEfConfigurationTests
    {
        private static TenantDbContext CreateTenantContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            return new TenantDbContext(options);
        }

        private static IEntityType FindEntity()
        {
            using var context = CreateTenantContext();
            var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CafeSettings));
            Assert.NotNull(entity);
            return entity!;
        }

        [Fact]
        public void CafeSettingsTable_HasXminRowVersionProperty()
        {
            var entity = FindEntity();

            var concurrencyToken = entity.FindProperty("xmin");

            Assert.NotNull(concurrencyToken);
            Assert.True(concurrencyToken.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, concurrencyToken.ValueGenerated);
        }

        [Fact]
        public void AddCafeSettingsConcurrencyTokenMigration_IsSnapshotOnly()
        {
            using var context = CreateTenantContext();
            var migrations = context.GetService<IMigrationsAssembly>();
            var migrationType = migrations.Migrations
                .Single(migration => migration.Key.EndsWith("_AddCafeSettingsConcurrencyToken", StringComparison.Ordinal))
                .Value;
            var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);

            Assert.Empty(migration.UpOperations);
            Assert.Empty(migration.DownOperations);
        }
    }
}
