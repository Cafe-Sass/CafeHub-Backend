using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddWifiVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wifi_vouchers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    imported_by = table.Column<Guid>(type: "uuid", nullable: true),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wifi_vouchers", x => x.id);
                    table.CheckConstraint("ck_wifi_vouchers_assignment_pair", "(status = 'Assigned') = (assigned_order_item_id IS NOT NULL AND assigned_at IS NOT NULL)");
                    table.CheckConstraint("ck_wifi_vouchers_code", "char_length(code) BETWEEN 4 AND 64 AND code ~ '^[A-Za-z0-9-]+$'");
                    table.CheckConstraint("ck_wifi_vouchers_status", "status IN ('Available', 'Assigned', 'Void')");
                    table.ForeignKey(
                        name: "fk_wifi_vouchers_assigned_order_item_id",
                        column: x => x.assigned_order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wifi_vouchers_imported_by",
                        column: x => x.imported_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_wifi_vouchers_package_id",
                        column: x => x.package_id,
                        principalTable: "wifi_packages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_wifi_vouchers_imported_by",
                table: "wifi_vouchers",
                column: "imported_by");

            migrationBuilder.CreateIndex(
                name: "uq_wifi_vouchers_assigned_order_item_id",
                table: "wifi_vouchers",
                column: "assigned_order_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_wifi_vouchers_package_id_code",
                table: "wifi_vouchers",
                columns: new[] { "package_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wifi_vouchers");
        }
    }
}
