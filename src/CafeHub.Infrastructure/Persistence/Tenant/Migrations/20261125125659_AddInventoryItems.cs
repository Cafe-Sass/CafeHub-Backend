using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    unit = table.Column<string>(type: "text", nullable: false),
                    low_stock_threshold = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    tracks_expiry = table.Column<bool>(type: "boolean", nullable: false),
                    near_expiry_days = table.Column<int>(type: "integer", nullable: false, defaultValue: 7),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_items", x => x.id);
                    table.CheckConstraint("ck_inventory_items_low_stock_threshold", "low_stock_threshold >= 0");
                    table.CheckConstraint("ck_inventory_items_name", "char_length(name) BETWEEN 1 AND 96 AND name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_inventory_items_near_expiry_days", "near_expiry_days >= 0");
                    table.CheckConstraint("ck_inventory_items_status", "status IN ('Active', 'Inactive')");
                    table.CheckConstraint("ck_inventory_items_unit", "unit IN ('Kg', 'G', 'L', 'Ml', 'Piece', 'Pack')");
                    table.ForeignKey(
                        name: "fk_inventory_items_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_items_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_created_by",
                table: "inventory_items",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_items_updated_by",
                table: "inventory_items",
                column: "updated_by");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_inventory_items_active_name
                ON inventory_items (lower(name))
                WHERE status = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_inventory_items_active_name;
                """);

            migrationBuilder.DropTable(
                name: "inventory_items");
        }
    }
}
