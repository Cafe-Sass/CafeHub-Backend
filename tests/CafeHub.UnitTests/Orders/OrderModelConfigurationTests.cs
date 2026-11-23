using CafeHub.Domain.Tenants;
using CafeHub.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CafeHub.UnitTests.Orders
{
    public sealed class OrderModelConfigurationTests
    {
        private readonly IModel model;

        public OrderModelConfigurationTests()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseNpgsql("Host=localhost;Database=test")
                .Options;
            using var context = new TenantDbContext(options);
            model = context.GetService<IDesignTimeModel>().Model;
        }

        [Fact]
        public void OrdersTable_MatchesTheErdNamePrimaryKeyAndConcurrencyToken()
        {
            var entity = Entity<Order>("orders");

            Assert.Equal("pk_orders", entity.FindPrimaryKey()!.GetName());
            Assert.Equal("id", Assert.Single(entity.FindPrimaryKey()!.Properties).GetColumnName());
            Assert.NotNull(entity.FindProperty("xmin"));
        }

        [Fact]
        public void OrdersTable_HasTheUniqueKeysAndLookupIndex()
        {
            var entity = Entity<Order>("orders");

            AssertUniqueIndex(entity, "uq_orders_branch_business_date_number", "branch_id", "business_date", "order_number");
            AssertUniqueIndex(entity, "uq_orders_public_reference", "public_reference");
            AssertUniqueIndex(entity, "uq_orders_branch_idempotency_key", "branch_id", "idempotency_key");
            var lookup = Assert.Single(entity.GetIndexes(), index => index.GetDatabaseName() == "ix_orders_branch_id_status_submitted_at");
            Assert.False(lookup.IsUnique);
            Assert.Equal(
                new[] { "branch_id", "status", "submitted_at" },
                lookup.Properties.Select(property => property.GetColumnName()).ToArray());
        }

        [Fact]
        public void OrdersTable_IndexesEveryForeignKeyAndChecksTheClosedSets()
        {
            var entity = Entity<Order>("orders");
            var foreignKeys = entity.GetForeignKeys().ToArray();

            Assert.All(foreignKeys, foreignKey => Assert.Contains(
                entity.GetIndexes(),
                index => index.Properties[0].Name == foreignKey.Properties[0].Name));
            Assert.Equal(
                new[]
                {
                    "bill_id", "branch_id", "created_by", "placed_by_user_id", "responded_by_user_id", "table_id",
                    "updated_by",
                },
                foreignKeys.Select(foreignKey => foreignKey.Properties.Single().GetColumnName()).OrderBy(name => name).ToArray());

            var checks = entity.GetCheckConstraints().Select(constraint => constraint.Name).ToArray();
            Assert.Contains("ck_orders_status", checks);
            Assert.Contains("ck_orders_service_type", checks);
            Assert.Contains("ck_orders_source", checks);
            Assert.Contains("ck_orders_public_reference_format", checks);
            Assert.Contains("ck_orders_guest_device_id_format", checks);
            Assert.Contains("ck_orders_total_amount_range", checks);
            Assert.Contains("ck_orders_response_deadline", checks);
        }

        [Fact]
        public void MoneyColumnsUseNumericTwelveTwo()
        {
            Assert.Equal("numeric(12,2)", Entity<Order>("orders").FindProperty("TotalAmount")!.GetColumnType());
            Assert.Equal("numeric(12,2)", Entity<OrderItem>("order_items").FindProperty("UnitPrice")!.GetColumnType());
            Assert.Equal("numeric(12,2)", Entity<OrderItem>("order_items").FindProperty("LineTotal")!.GetColumnType());
            Assert.Equal("numeric(12,2)", Entity<OrderItemAddon>("order_item_addons").FindProperty("Price")!.GetColumnType());
        }

        [Fact]
        public void OrderItemsAndAddons_UseTheirSnapshotsAndIndexedReferences()
        {
            var item = Entity<OrderItem>("order_items");
            Assert.Equal("pk_order_items", item.FindPrimaryKey()!.GetName());
            AssertUniqueIndex(item, "uq_order_items_order_id_line_number", "order_id", "line_number");
            Assert.All(
                new[] { "order_id", "product_id", "product_size_id", "station_id" },
                column => Assert.Contains(item.GetIndexes(), index =>
                    index.Properties.Single().GetColumnName() == column));
            Assert.Equal("date", Entity<Order>("orders").FindProperty("BusinessDate")!.GetColumnType());

            var addon = Entity<OrderItemAddon>("order_item_addons");
            Assert.Equal("pk_order_item_addons", addon.FindPrimaryKey()!.GetName());
            Assert.True(addon.FindProperty("ProductAddonId")!.IsNullable);
            Assert.Contains(addon.GetIndexes(), index =>
                index.Properties.Single().GetColumnName() == "order_item_id");
        }

        [Fact]
        public void CountersTable_IsKeyedByBranchAndBusinessDate()
        {
            var entity = Entity<OrderNumberCounter>("order_number_counters");
            var key = entity.FindPrimaryKey()!;

            Assert.Equal("pk_order_number_counters", key.GetName());
            Assert.Equal(
                new[] { "branch_id", "business_date" },
                key.Properties.Select(property => property.GetColumnName()).ToArray());
            Assert.Contains(entity.GetCheckConstraints(), constraint =>
                constraint.Name == "ck_order_number_counters_last_number");
        }

        private IEntityType Entity<T>(string tableName)
        {
            var entity = model.FindEntityType(typeof(T));
            Assert.NotNull(entity);
            Assert.Equal(tableName, entity!.GetTableName());
            return entity;
        }

        private static void AssertUniqueIndex(IEntityType entity, string name, params string[] columns)
        {
            var index = Assert.Single(entity.GetIndexes(), candidate => candidate.GetDatabaseName() == name);
            Assert.True(index.IsUnique);
            Assert.Equal(columns, index.Properties.Select(property => property.GetColumnName()).ToArray());
        }
    }
}
