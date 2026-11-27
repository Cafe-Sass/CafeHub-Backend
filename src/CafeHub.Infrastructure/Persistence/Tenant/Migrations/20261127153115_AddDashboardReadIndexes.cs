using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_payments_status_confirmed_at",
                table: "payments",
                columns: new[] { "status", "confirmed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_branch_id_status_responded_at",
                table: "orders",
                columns: new[] { "branch_id", "status", "responded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_status_confirmed_at",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "ix_orders_branch_id_status_responded_at",
                table: "orders");
        }
    }
}
