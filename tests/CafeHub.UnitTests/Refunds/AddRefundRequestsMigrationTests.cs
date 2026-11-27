using System.Reflection;
using CafeHub.Infrastructure.Persistence.Tenant.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Refunds
{
    public sealed class AddRefundRequestsMigrationTests
    {
        [Fact]
        public void Up_CreatesOnlyRefundRequestTablesAndIndexes()
        {
            var operations = Operations("Up");

            Assert.Equal(
                ["refund_request_attachments", "refund_request_events", "refund_requests"],
                operations.OfType<CreateTableOperation>().Select(operation => operation.Name).OrderBy(name => name).ToArray());
            Assert.All(operations, operation => Assert.True(
                operation is CreateTableOperation or CreateIndexOperation,
                operation.GetType().Name));
            Assert.DoesNotContain(
                operations.OfType<CreateTableOperation>(),
                operation => operation.Name is "bills" or "payments" or "orders" or "receipts");
        }

        [Fact]
        public void Up_AddsRequestIndexesAndOnlyReferencesExistingBillingTables()
        {
            var operations = Operations("Up");
            var requests = Assert.Single(operations.OfType<CreateTableOperation>(), operation => operation.Name == "refund_requests");

            Assert.Single(
                operations.OfType<CreateIndexOperation>(),
                operation => operation.Name == "uq_refund_requests_branch_idempotency_key" && operation.IsUnique);
            Assert.Single(
                operations.OfType<CreateIndexOperation>(),
                operation => operation.Name == "uq_refund_requests_public_reference" && operation.IsUnique);
            Assert.Contains(requests.ForeignKeys, foreignKey => foreignKey.Name == "fk_refund_requests_bill_id" && foreignKey.PrincipalTable == "bills");
            Assert.Contains(requests.ForeignKeys, foreignKey => foreignKey.Name == "fk_refund_requests_payment_id" && foreignKey.PrincipalTable == "payments");
            Assert.Contains(requests.ForeignKeys, foreignKey => foreignKey.Name == "fk_refund_requests_order_id" && foreignKey.PrincipalTable == "orders");
        }

        [Fact]
        public void Down_DropsOnlyRefundRequestTables()
        {
            var operations = Operations("Down");

            Assert.Equal(
                ["refund_request_attachments", "refund_request_events", "refund_requests"],
                operations.OfType<DropTableOperation>().Select(operation => operation.Name).OrderBy(name => name).ToArray());
            Assert.All(operations, operation => Assert.IsType<DropTableOperation>(operation));
        }

        private static IReadOnlyList<MigrationOperation> Operations(string methodName)
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            var method = typeof(Migration).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(new AddRefundRequests(), [builder]);
            return builder.Operations;
        }
    }
}
