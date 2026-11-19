using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace CafeHub.UnitTests.Tenants.Persistence
{
    public sealed class AddMenuProductsMigrationTests
    {
        [Fact]
        public void Migration_AddsOnlyProductSchemaAndCafeCurrency_AndReversesAllItsOperations()
        {
            using var context = new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=192.0.2.1;Database=offline").Options);
            var migrator = context.GetService<IMigrator>();
            const string predecessor = "20261116061125_AddMenuCategories";
            const string target = "20261116122028_AddMenuProducts";

            var up = migrator.GenerateScript(predecessor, target);
            var down = migrator.GenerateScript(target, predecessor);

            Assert.Contains("ALTER TABLE cafe_settings ADD currency_code text NOT NULL DEFAULT 'egp'", up, StringComparison.Ordinal);
            Assert.Contains("ck_cafe_settings_currency_code_format", up);
            Assert.Contains("currency_code ~ '^[a-z]{3}$'", up);

            Assert.Contains("CREATE TABLE products (", up, StringComparison.Ordinal);
            Assert.Contains("CREATE TABLE product_sizes (", up, StringComparison.Ordinal);
            Assert.Contains("CREATE TABLE product_addons (", up, StringComparison.Ordinal);
            Assert.Contains("CREATE TABLE product_tags (", up, StringComparison.Ordinal);
            Assert.Contains("fk_products_category_id FOREIGN KEY (category_id) REFERENCES menu_categories (id) ON DELETE RESTRICT", up);
            Assert.Contains("fk_products_station_id", up);
            Assert.Contains("fk_products_created_by", up);
            Assert.Contains("fk_products_updated_by", up);
            Assert.Contains("fk_product_sizes_product_id", up);
            Assert.Contains("fk_product_addons_product_id", up);
            Assert.Contains("fk_product_tags_product_id", up);
            Assert.Contains("char_length(name) BETWEEN 1 AND 96", up);
            Assert.Contains("kind IN ('Food', 'Drink')", up);
            Assert.Contains("status IN ('Active', 'Inactive')", up);
            Assert.Contains("char_length(name) BETWEEN 1 AND 32", up);
            Assert.Contains("char_length(name) BETWEEN 1 AND 48", up);
            Assert.Contains("price >= 0 AND price <= 99999999.99", up);
            Assert.Contains("tag ~ '^[a-z0-9 -]+$'", up);
            Assert.Contains("tag = lower(tag)", up);
            Assert.Contains("ix_products_category_id", up);
            Assert.Contains("ix_product_sizes_product_id", up);
            Assert.Contains("ix_product_addons_product_id", up);
            Assert.Contains("ix_product_tags_tag", up);
            Assert.DoesNotContain("xmin xid", up);

            Assert.Contains("ON product_sizes (product_id)", up);
            Assert.Contains("WHERE is_default AND status = 'Active'", up);
            Assert.Contains("ON product_sizes (product_id, lower(name))", up);
            Assert.Contains("ON product_addons (product_id, lower(name))", up);
            Assert.Contains("WHERE status = 'Active'", up);

            Assert.Contains(target, up);
            Assert.DoesNotContain("CREATE TABLE menu_categories", up);
            Assert.DoesNotContain("CREATE TABLE branch_tables", up);
            Assert.DoesNotContain("CREATE TABLE stations", up);

            Assert.Contains("DROP INDEX IF EXISTS uq_product_addons_active_name", down);
            Assert.Contains("DROP INDEX IF EXISTS uq_product_sizes_active_name", down);
            Assert.Contains("DROP INDEX IF EXISTS uq_product_sizes_active_default", down);
            Assert.Contains("DROP TABLE product_addons", down);
            Assert.Contains("DROP TABLE product_sizes", down);
            Assert.Contains("DROP TABLE product_tags", down);
            Assert.Contains("DROP TABLE products", down);
            Assert.Contains("DROP COLUMN currency_code", down);
            Assert.Contains(target, down);
            Assert.DoesNotContain("DROP TABLE tenant_users", down);
            Assert.False(migrator.HasPendingModelChanges());

            var product = context.Model.FindEntityType(typeof(Product))!;
            Assert.Equal("products", product.GetTableName());
            Assert.True(product.FindProperty("xmin")!.IsConcurrencyToken);
            Assert.All(product.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));
            Assert.Equal(
                new[] { "Sizes", "Tags", "Addons" }.OrderBy(name => name),
                product.GetNavigations().Select(navigation => navigation.Name).OrderBy(name => name));

            var size = context.Model.FindEntityType(typeof(ProductSize))!;
            Assert.Equal("numeric(12,2)", size.FindProperty(nameof(ProductSize.Price))!.GetColumnType());
            Assert.All(size.GetForeignKeys(), key => Assert.Equal(DeleteBehavior.Restrict, key.DeleteBehavior));

            var addon = context.Model.FindEntityType(typeof(ProductAddon))!;
            Assert.Equal("numeric(12,2)", addon.FindProperty(nameof(ProductAddon.Price))!.GetColumnType());

            var tag = context.Model.FindEntityType(typeof(ProductTag))!;
            Assert.Equal(new[] { nameof(ProductTag.Id) }, tag.FindPrimaryKey()!.Properties.Select(p => p.Name));

            var settings = context.Model.FindEntityType(typeof(CafeSettings))!;
            Assert.Equal(CafeSettings.DefaultCurrencyCode, settings.FindProperty(nameof(CafeSettings.CurrencyCode))!.GetDefaultValue());
        }
    }
}
