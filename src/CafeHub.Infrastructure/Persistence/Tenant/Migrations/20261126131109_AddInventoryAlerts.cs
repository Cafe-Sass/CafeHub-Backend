using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    notified_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_alerts", x => x.id);
                    table.CheckConstraint("ck_inventory_alerts_batch", "((kind = 'LowStock' AND batch_id IS NULL) OR (kind IN ('NearExpiry', 'Expired') AND batch_id IS NOT NULL))");
                    table.CheckConstraint("ck_inventory_alerts_kind", "kind IN ('LowStock', 'NearExpiry', 'Expired')");
                    table.CheckConstraint("ck_inventory_alerts_notified_not_before_raised", "notified_at IS NULL OR notified_at >= raised_at");
                    table.CheckConstraint("ck_inventory_alerts_resolved", "((state = 'Open' AND resolved_at IS NULL) OR (state = 'Resolved' AND resolved_at IS NOT NULL))");
                    table.CheckConstraint("ck_inventory_alerts_resolved_not_before_raised", "resolved_at IS NULL OR resolved_at >= raised_at");
                    table.CheckConstraint("ck_inventory_alerts_state", "state IN ('Open', 'Resolved')");
                    table.ForeignKey(
                        name: "fk_inventory_alerts_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_alerts_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_alerts_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_alerts_item_id",
                        column: x => x.item_id,
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_alerts_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_alerts_batch_id",
                table: "inventory_alerts",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_alerts_branch_id",
                table: "inventory_alerts",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_alerts_created_by",
                table: "inventory_alerts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_alerts_item_id",
                table: "inventory_alerts",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_alerts_updated_by",
                table: "inventory_alerts",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_alerts_open_batch",
                table: "inventory_alerts",
                columns: new[] { "item_id", "branch_id", "kind", "batch_id" },
                unique: true,
                filter: "state = 'Open' AND batch_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_alerts_open_item",
                table: "inventory_alerts",
                columns: new[] { "item_id", "branch_id", "kind" },
                unique: true,
                filter: "state = 'Open' AND batch_id IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_alerts");
        }
    }
}
