using System.Reflection;
using CafeHub.Infrastructure.Persistence.Tenant.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Billing
{
    public sealed class AddBillsMigrationTests
    {
        [Fact]
        public void Up_CreatesOnlyTheBillsTableAndTheOrdersAttachment()
        {
            var operations = Operations("Up");

            var created = operations.OfType<CreateTableOperation>().Select(operation => operation.Name).ToArray();
            Assert.Equal(["bills"], created);
            Assert.All(operations, operation => Assert.True(
                operation is CreateTableOperation or CreateIndexOperation or AddColumnOperation
                    or AddForeignKeyOperation or SqlOperation,
                operation.GetType().Name));
        }

        [Fact]
        public void Up_AddsTheOneOpenBillPerTableIndexAndTheOrderAttachmentForeignKey()
        {
            var operations = Operations("Up");

            var openIndex = Assert.Single(
                operations.OfType<CreateIndexOperation>(),
                index => index.Name == "uq_bills_table_open");
            Assert.True(openIndex.IsUnique);
            Assert.Equal(["table_id"], openIndex.Columns);
            Assert.Equal("status = 'Open'", openIndex.Filter);

            Assert.Single(
                operations.OfType<AddColumnOperation>(),
                operation => operation.Table == "orders" && operation.Name == "bill_id");
            var foreignKey = Assert.Single(
                operations.OfType<AddForeignKeyOperation>(),
                operation => operation.Name == "fk_orders_bill_id");
            Assert.Equal("orders", foreignKey.Table);
            Assert.Equal("bills", foreignKey.PrincipalTable);
        }

        [Fact]
        public void Up_AddsThePublicReferenceAndForeignKeysWithTheirIndexes()
        {
            var operations = Operations("Up");

            Assert.Single(
                operations.OfType<CreateIndexOperation>(),
                index => index.Name == "uq_bills_public_reference" && index.IsUnique);
            Assert.Equal(
                new[]
                {
                    "ix_bills_branch_id",
                    "ix_bills_closed_by_user_id",
                    "ix_bills_created_by",
                    "ix_bills_updated_by",
                },
                operations.OfType<CreateIndexOperation>()
                    .Where(index => index.Table == "bills" && index.Name.StartsWith("ix_bills_", StringComparison.Ordinal))
                    .Select(index => index.Name)
                    .OrderBy(name => name)
                    .ToArray());

            var bills = Assert.Single(operations.OfType<CreateTableOperation>(), table => table.Name == "bills");
            Assert.Contains(bills.ForeignKeys, foreignKey => foreignKey.Name == "fk_bills_branch_id" && foreignKey.PrincipalTable == "branches");
            Assert.Contains(bills.ForeignKeys, foreignKey => foreignKey.Name == "fk_bills_table_id" && foreignKey.PrincipalTable == "branch_tables");
            Assert.Equal(5, bills.ForeignKeys.Count);
            Assert.Contains(
                operations.OfType<SqlOperation>(),
                operation => operation.Sql.Contains("CREATE INDEX ix_bills_table_id", StringComparison.Ordinal));
        }

        [Fact]
        public void Down_DropsExactlyTheBillsTableAndTheOrdersAttachment()
        {
            var operations = Operations("Down");

            var dropped = operations.OfType<DropTableOperation>().Select(operation => operation.Name).ToArray();
            Assert.Equal(["bills"], dropped);
            Assert.Single(operations.OfType<DropForeignKeyOperation>(), operation => operation.Name == "fk_orders_bill_id");
            Assert.Single(operations.OfType<DropIndexOperation>(), operation => operation.Table == "orders" && operation.Name == "ix_orders_bill_id");
            Assert.Single(operations.OfType<DropColumnOperation>(), operation => operation.Table == "orders" && operation.Name == "bill_id");
            Assert.Single(
                operations.OfType<SqlOperation>(),
                operation => operation.Sql.Contains("DROP INDEX ix_bills_table_id", StringComparison.Ordinal));
            Assert.All(operations, operation => Assert.True(
                operation is DropTableOperation or DropIndexOperation or DropColumnOperation
                    or DropForeignKeyOperation or SqlOperation,
                operation.GetType().Name));
        }

        private static IReadOnlyList<MigrationOperation> Operations(string methodName)
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            var method = typeof(Migration).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(new AddBills(), [builder]);
            return builder.Operations;
        }
    }
}
