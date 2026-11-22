using System.Reflection;
using CafeHub.Infrastructure.Persistence.Tenant.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Orders
{
    public sealed class AddOrdersMigrationTests
    {
        private static readonly string[] CreatedTables =
        [
            "order_item_addons", "order_items", "order_number_counters", "orders",
        ];

        [Fact]
        public void Up_CreatesOnlyTheFourOrderTables()
        {
            var operations = Operations("Up");

            var created = operations.OfType<CreateTableOperation>().Select(operation => operation.Name).OrderBy(name => name).ToArray();
            Assert.Equal(CreatedTables, created);
            Assert.All(operations, operation =>
                Assert.True(operation is CreateTableOperation or CreateIndexOperation, operation.GetType().Name));
        }

        [Fact]
        public void Up_AddsTheUniqueKeysChecksAndForeignKeys()
        {
            var operations = Operations("Up");
            var uniqueIndexes = operations.OfType<CreateIndexOperation>().Where(index => index.IsUnique)
                .Select(index => index.Name).ToArray();
            Assert.Contains("uq_orders_branch_business_date_number", uniqueIndexes);
            Assert.Contains("uq_orders_public_reference", uniqueIndexes);
            Assert.Contains("uq_orders_branch_idempotency_key", uniqueIndexes);
            Assert.Contains("uq_order_items_order_id_line_number", uniqueIndexes);

            var orders = Assert.Single(operations.OfType<CreateTableOperation>(), table => table.Name == "orders");
            var checks = orders.CheckConstraints.Select(constraint => constraint.Name).ToArray();
            Assert.Contains("ck_orders_status", checks);
            Assert.Contains("ck_orders_service_type", checks);
            Assert.Contains("ck_orders_source", checks);
            Assert.Contains("ck_orders_total_amount_range", checks);
            Assert.Contains("ck_orders_public_reference_format", checks);
            Assert.Contains(
                orders.ForeignKeys,
                foreignKey => foreignKey.Name == "fk_orders_branch_id" && foreignKey.PrincipalTable == "branches");
            Assert.Contains(
                orders.ForeignKeys,
                foreignKey => foreignKey.Name == "fk_orders_table_id" && foreignKey.PrincipalTable == "branch_tables");
            Assert.Equal(
                "numeric(12,2)",
                Assert.Single(orders.Columns, column => column.Name == "total_amount").ColumnType);
        }

        [Fact]
        public void Down_DropsExactlyTheFourOrderTables()
        {
            var operations = Operations("Down");

            var dropped = operations.OfType<DropTableOperation>().Select(operation => operation.Name).OrderBy(name => name).ToArray();
            Assert.Equal(CreatedTables, dropped);
            Assert.All(operations, operation => Assert.IsType<DropTableOperation>(operation));
        }

        private static IReadOnlyList<MigrationOperation> Operations(string methodName)
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            var method = typeof(Migration).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(new AddOrders(), [builder]);
            return builder.Operations;
        }
    }
}
