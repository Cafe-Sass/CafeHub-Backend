using System.Reflection;
using CafeHub.Infrastructure.Persistence.Tenant.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CafeHub.UnitTests.Guests
{
    public sealed class AddGuestMutesMigrationTests
    {
        [Fact]
        public void Up_CreatesOnlyTheGuestMuteTable()
        {
            var operations = Operations("Up");

            var created = Assert.Single(operations.OfType<CreateTableOperation>());
            Assert.Equal("guest_mutes", created.Name);
            Assert.All(operations, operation =>
                Assert.True(operation is CreateTableOperation or CreateIndexOperation, operation.GetType().Name));
        }

        [Fact]
        public void Up_AddsTheBranchDeviceAndActingUserIndexes()
        {
            var operations = Operations("Up");

            var indexNames = operations.OfType<CreateIndexOperation>().Select(index => index.Name).ToArray();
            Assert.Contains("ix_guest_mutes_branch_device", indexNames);
            Assert.Contains("ix_guest_mutes_muted_by_user_id", indexNames);
            Assert.Contains("ix_guest_mutes_lifted_by_user_id", indexNames);
            Assert.All(indexNames, name => Assert.StartsWith("ix_guest_mutes_", name, StringComparison.Ordinal));

            var branchDevice = Assert.Single(operations.OfType<CreateIndexOperation>(),
                index => index.Name == "ix_guest_mutes_branch_device");
            Assert.Equal(["branch_id", "device_id"], branchDevice.Columns);

            var table = Assert.Single(operations.OfType<CreateTableOperation>());
            Assert.Contains(table.ForeignKeys, foreignKey => foreignKey.Name == "fk_guest_mutes_branch_id");
            Assert.Contains(table.ForeignKeys, foreignKey => foreignKey.Name == "fk_guest_mutes_order_id");
            Assert.Contains(table.ForeignKeys, foreignKey => foreignKey.Name == "fk_guest_mutes_muted_by_user_id");
            Assert.Contains(table.Columns, column => column.Name == "lifted_at" && column.IsNullable);
        }

        [Fact]
        public void Down_DropsOnlyTheGuestMuteTable()
        {
            var operations = Operations("Down");

            var dropped = Assert.Single(operations.OfType<DropTableOperation>());
            Assert.Equal("guest_mutes", dropped.Name);
        }

        private static IReadOnlyList<MigrationOperation> Operations(string methodName)
        {
            var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
            var method = typeof(Migration).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!;
            method.Invoke(new AddGuestMutes(), [builder]);
            return builder.Operations;
        }
    }
}
