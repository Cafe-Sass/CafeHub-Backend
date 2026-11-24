using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "receipt_number_counters",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_number_counters", x => new { x.branch_id, x.business_date });
                    table.CheckConstraint("ck_receipt_number_counters_last_number", "last_number >= 1");
                    table.ForeignKey(
                        name: "fk_receipt_number_counters_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_number = table.Column<string>(type: "text", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    paid_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    remaining_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipts", x => x.id);
                    table.CheckConstraint("ck_receipts_fully_paid", "remaining_amount = 0");
                    table.CheckConstraint("ck_receipts_paid_amount_range", "paid_amount > 0");
                    table.CheckConstraint("ck_receipts_receipt_number_format", "char_length(receipt_number) BETWEEN 11 AND 32 AND receipt_number ~ '^R-[0-9]{8}-[0-9]+$'");
                    table.CheckConstraint("ck_receipts_total_amount_range", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_receipts_bill_id",
                        column: x => x.bill_id,
                        principalTable: "bills",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipts_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_receipts_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    guest_display_name = table.Column<string>(type: "text", nullable: false),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    size_name = table.Column<string>(type: "text", nullable: false),
                    addons = table.Column<string>(type: "text", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_lines", x => x.id);
                    table.CheckConstraint("ck_receipt_lines_addons", "addons IS NULL OR (char_length(addons) BETWEEN 1 AND 512 AND addons !~ '[[:cntrl:]]')");
                    table.CheckConstraint("ck_receipt_lines_guest_name", "char_length(guest_display_name) BETWEEN 1 AND 64 AND guest_display_name !~ '[[:cntrl:]]'");
                    table.CheckConstraint("ck_receipt_lines_line_total", "line_total >= 0 AND line_total <= 99999999.99");
                    table.CheckConstraint("ck_receipt_lines_order_number", "order_number >= 1");
                    table.CheckConstraint("ck_receipt_lines_product_name", "char_length(product_name) BETWEEN 1 AND 96");
                    table.CheckConstraint("ck_receipt_lines_quantity", "quantity BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_receipt_lines_size_name", "char_length(size_name) BETWEEN 1 AND 32");
                    table.CheckConstraint("ck_receipt_lines_unit_price", "unit_price >= 0 AND unit_price <= 99999999.99");
                    table.ForeignKey(
                        name: "fk_receipt_lines_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "receipt_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    receipt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_receipt_payments", x => x.id);
                    table.CheckConstraint("ck_receipt_payments_amount_range", "amount > 0");
                    table.CheckConstraint("ck_receipt_payments_method", "method IN ('Cash', 'Online')");
                    table.ForeignKey(
                        name: "fk_receipt_payments_receipt_id",
                        column: x => x.receipt_id,
                        principalTable: "receipts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_receipt_lines_receipt_id",
                table: "receipt_lines",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipt_payments_receipt_id",
                table: "receipt_payments",
                column: "receipt_id");

            migrationBuilder.CreateIndex(
                name: "ix_receipts_branch_issued_at",
                table: "receipts",
                columns: new[] { "branch_id", "issued_at" });

            migrationBuilder.CreateIndex(
                name: "ix_receipts_created_by",
                table: "receipts",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "uq_receipts_bill_id",
                table: "receipts",
                column: "bill_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_receipts_branch_receipt_number",
                table: "receipts",
                columns: new[] { "branch_id", "receipt_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "receipt_lines");

            migrationBuilder.DropTable(
                name: "receipt_number_counters");

            migrationBuilder.DropTable(
                name: "receipt_payments");

            migrationBuilder.DropTable(
                name: "receipts");
        }
    }
}
