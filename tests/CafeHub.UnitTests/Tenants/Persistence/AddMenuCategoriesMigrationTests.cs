using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class AddMenuCategoriesMigrationTests
    {
        [Fact]
        public void Migration_AddsOnlyMenuSchemaAndReversesAllItsOperations()
        {
            using var context = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=offline").Options);
            var migrator = context.GetService<IMigrator>();
            const string predecessor = "20261116011824_AddStaffStationAssignments";
            const string target = "20261116061125_AddMenuCategories";

            var up = migrator.GenerateScript(predecessor, target);
            var down = migrator.GenerateScript(target, predecessor);

            Assert.Contains("CREATE TABLE menu_categories (", up, StringComparison.Ordinal);
            Assert.Contains("fk_menu_categories_parent_id FOREIGN KEY (parent_id) REFERENCES menu_categories (id) ON DELETE RESTRICT", up);
            Assert.Contains("fk_menu_categories_created_by", up);
            Assert.Contains("fk_menu_categories_updated_by", up);
            Assert.Contains("char_length(name) BETWEEN 1 AND 64", up);
            Assert.Contains("sort_order >= 0", up);
            Assert.Contains("parent_id <> id", up);
            Assert.Contains("status IN ('Active', 'Inactive')", up);
            Assert.DoesNotContain("xmin xid", up);
            Assert.Contains("ix_menu_categories_parent_id_sort_order_id ON menu_categories (parent_id, sort_order, id)", up);
            Assert.Contains("ON menu_categories (lower(name))", up);
            Assert.Contains("WHERE parent_id IS NULL AND status = 'Active'", up);
            Assert.Contains("ON menu_categories (parent_id, lower(name))", up);
            Assert.Contains("WHERE parent_id IS NOT NULL AND status = 'Active'", up);
            Assert.Contains(target, up);
            Assert.DoesNotContain("ALTER TABLE", up);
            Assert.DoesNotContain("CREATE TABLE branch_tables", up);
            Assert.DoesNotContain("CREATE TABLE stations", up);
            Assert.Contains("DROP INDEX IF EXISTS uq_menu_categories_active_child_name", down);
            Assert.Contains("DROP INDEX IF EXISTS uq_menu_categories_active_root_name", down);
            Assert.Contains("DROP TABLE menu_categories", down);
            Assert.Contains(target, down);
            Assert.DoesNotContain("DROP TABLE tenant_users", down);
            Assert.False(migrator.HasPendingModelChanges());

            var entity = context.Model.FindEntityType(typeof(MenuCategory))!;
            Assert.True(entity.FindProperty("xmin")!.IsConcurrencyToken);
            Assert.All(entity.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
            Assert.Contains(context.Model.GetEntityTypes(), type => type.GetTableName() == "stations");
            Assert.Contains(context.Model.GetEntityTypes(), type => type.GetTableName() == "tenant_user_stations");
            Assert.Contains(context.Model.FindEntityType(typeof(BranchTable))!.GetIndexes(),
                index => index.IsUnique && index.GetDatabaseName() == "uq_branch_tables_qr_code");
        }
    }
}
