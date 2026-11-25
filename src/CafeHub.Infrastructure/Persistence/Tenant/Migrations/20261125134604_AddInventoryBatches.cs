using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    remaining_quantity = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    received_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_batches", x => x.id);
                    table.CheckConstraint("ck_inventory_batches_expiry_not_before_received", "expiry_date IS NULL OR expiry_date >= received_date");
                    table.CheckConstraint("ck_inventory_batches_idempotency_key_length", "char_length(idempotency_key) BETWEEN 8 AND 64");
                    table.CheckConstraint("ck_inventory_batches_received_quantity", "received_quantity >= 0");
                    table.CheckConstraint("ck_inventory_batches_remaining_quantity", "remaining_quantity >= 0 AND remaining_quantity <= received_quantity");
                    table.ForeignKey(
                        name: "fk_inventory_batches_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_batches_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_batches_item_id",
                        column: x => x.item_id,
                        principalTable: "inventory_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_batches_received_by_user_id",
                        column: x => x.received_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_inventory_batches_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_created_by",
                table: "inventory_batches",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_item_id",
                table: "inventory_batches",
                column: "item_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_received_by_user_id",
                table: "inventory_batches",
                column: "received_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_inventory_batches_updated_by",
                table: "inventory_batches",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_inventory_batches_branch_idempotency_key",
                table: "inventory_batches",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_batches");
        }
    }
}
