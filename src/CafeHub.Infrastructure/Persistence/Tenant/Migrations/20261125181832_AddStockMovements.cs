using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddStockMovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "stock_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_idempotency_key_length", "char_length(idempotency_key) BETWEEN 8 AND 64");
                    table.CheckConstraint("ck_stock_movements_quantity", "quantity <> 0 AND quantity >= -999999999.999 AND quantity <= 999999999.999");
                    table.CheckConstraint("ck_stock_movements_quantity_sign", "((type = 'Receive' AND quantity > 0) OR (type IN ('Consume', 'Waste') AND quantity < 0) OR (type = 'Adjust' AND quantity <> 0))");
                    table.CheckConstraint("ck_stock_movements_reason", "reason IS NULL OR (char_length(btrim(reason)) BETWEEN 1 AND 500 AND reason !~ '[[:cntrl:]]')");
                    table.CheckConstraint("ck_stock_movements_reason_required", "type = 'Receive' OR reason IS NOT NULL");
                    table.CheckConstraint("ck_stock_movements_type", "type IN ('Receive', 'Consume', 'Waste', 'Adjust')");
                    table.ForeignKey(
                        name: "fk_stock_movements_batch_id",
                        column: x => x.batch_id,
                        principalTable: "inventory_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_item_id",
                        column: x => x.item_id,
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_batch_id",
                table: "stock_movements",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_branch_id",
                table: "stock_movements",
                column: "branch_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_created_by",
                table: "stock_movements",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_item_id",
                table: "stock_movements",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "uq_stock_movements_branch_key_batch",
                table: "stock_movements",
                columns: new[] { "branch_id", "idempotency_key", "batch_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "stock_movements");
        }
    }
}
