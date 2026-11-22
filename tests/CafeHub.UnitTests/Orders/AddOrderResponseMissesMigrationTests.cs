using System.Reflection;
using CafeHub.Infrastructure.Persistence.Tenant.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Orders
{
    public sealed class AddOrderResponseMissesMigrationTests
    {
        private static readonly string[] CreatedTables =
        [
            "order_response_miss_holders", "order_response_misses",
        ];

        [Fact]
        public void Up_CreatesOnlyTheTwoMissTables()
        {
            var operations = Operations("Up");

            var created = operations.OfType<CreateTableOperation>().Select(operation => operation.Name).OrderBy(name => name).ToArray();
            Assert.Equal(CreatedTables, created);
            Assert.All(operations, operation =>
                Assert.True(operation is CreateTableOperation or CreateIndexOperation, operation.GetType().Name));
        }

        [Fact]
        public void Up_AddsTheUniqueOrderKeyAndTheSummaryIndex()
        {
            var operations = Operations("Up");

            var uniqueIndexes = operations.OfType<CreateIndexOperation>().Where(index => index.IsUnique)
                .Select(index => index.Name).ToArray();
            Assert.Equal(["uq_order_response_misses_order_id"], uniqueIndexes);

            var misses = Assert.Single(operations.OfType<CreateTableOperation>(), table => table.Name == "order_response_misses");
            Assert.Contains(
                misses.ForeignKeys,
                foreignKey => foreignKey.Name == "fk_order_response_misses_order_id" && foreignKey.PrincipalTable == "orders");
            Assert.Equal(
                new[] { "branch_id", "expired_at" },
                Assert.Single(operations.OfType<CreateIndexOperation>(), index => index.Name == "ix_order_response_misses_branch_expired_at").Columns);
        }

        [Fact]
        public void Down_DropsExactlyTheTwoMissTables()
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
            method.Invoke(new AddOrderResponseMisses(), [builder]);
            return builder.Operations;
        }
    }
}
