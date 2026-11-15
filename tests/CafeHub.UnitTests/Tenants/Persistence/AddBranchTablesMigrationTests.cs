using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class AddBranchTablesMigrationTests
    {
        private static TenantDbContext CreateTenantContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            return new TenantDbContext(options);
        }

        [Fact]
        public void AddBranchTablesMigration_ContainsOnlyTheIntendedDdl()
        {
            using var context = CreateTenantContext();
            var migrations = context.GetService<IMigrationsAssembly>();
            var migrationType = migrations.Migrations
                .Single(migration => migration.Key == "20261115190000_AddBranchTables")
                .Value;
            var migration = migrations.CreateMigration(migrationType, context.Database.ProviderName!);

            var up = migration.UpOperations;
            Assert.Equal(6, up.Count);
            Assert.Single(up, op => op.GetType().Name == "CreateTableOperation");
            Assert.Equal(4, up.Count(op => op.GetType().Name == "CreateIndexOperation"));
            Assert.Single(up, op => op.GetType().Name == "SqlOperation");

            var down = migration.DownOperations;
            Assert.Equal(2, down.Count);
            Assert.Single(down, op => op.GetType().Name == "SqlOperation");
            Assert.Single(down, op => op.GetType().Name == "DropTableOperation");
        }
    }
}
