using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "order_number_counters",
                columns: table => new
                {
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    last_number = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_number_counters", x => new { x.branch_id, x.business_date });
                    table.CheckConstraint("ck_order_number_counters_last_number", "last_number >= 1");
                    table.ForeignKey(
                        name: "fk_order_number_counters_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    table_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<int>(type: "integer", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    service_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    guest_display_name = table.Column<string>(type: "text", nullable: false),
                    guest_device_id = table.Column<string>(type: "text", nullable: true),
                    placed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    public_reference = table.Column<string>(type: "text", nullable: false),
                    idempotency_key = table.Column<string>(type: "text", nullable: false),
                    request_hash = table.Column<string>(type: "text", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    response_deadline = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    responded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.CheckConstraint("ck_orders_decision_pair", "(responded_at IS NULL AND responded_by_user_id IS NULL AND rejection_reason IS NULL) OR (responded_at IS NOT NULL AND responded_by_user_id IS NOT NULL)");
                    table.CheckConstraint("ck_orders_guest_device_id_format", "guest_device_id IS NULL OR (char_length(guest_device_id) BETWEEN 16 AND 64 AND guest_device_id ~ '^[A-Za-z0-9_-]+$')");
                    table.CheckConstraint("ck_orders_guest_display_name_length", "char_length(guest_display_name) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_orders_idempotency_key_length", "char_length(idempotency_key) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_orders_order_number", "order_number >= 1");
                    table.CheckConstraint("ck_orders_public_reference_format", "char_length(public_reference) = 22 AND public_reference ~ '^[A-Za-z0-9_-]{22}$'");
                    table.CheckConstraint("ck_orders_rejection_reason", "rejection_reason IS NULL OR (status = 'Rejected' AND char_length(rejection_reason) BETWEEN 1 AND 256)");
                    table.CheckConstraint("ck_orders_request_hash_format", "request_hash IS NULL OR request_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_orders_response_deadline", "response_deadline > submitted_at");
                    table.CheckConstraint("ck_orders_service_type", "service_type IN ('DineIn', 'Takeaway')");
                    table.CheckConstraint("ck_orders_source", "source IN ('Guest', 'Captain')");
                    table.CheckConstraint("ck_orders_status", "status IN ('Pending', 'Accepted', 'Rejected', 'Expired')");
                    table.CheckConstraint("ck_orders_total_amount_range", "total_amount >= 0");
                    table.ForeignKey(
                        name: "fk_orders_branch_id",
                        column: x => x.branch_id,
                        principalTable: "branches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_placed_by_user_id",
                        column: x => x.placed_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_responded_by_user_id",
                        column: x => x.responded_by_user_id,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_table_id",
                        column: x => x.table_id,
                        principalTable: "branch_tables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_orders_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_size_id = table.Column<Guid>(type: "uuid", nullable: true),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    size_name = table.Column<string>(type: "text", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.CheckConstraint("ck_order_items_line_number", "line_number >= 1");
                    table.CheckConstraint("ck_order_items_line_total_range", "line_total >= 0 AND line_total <= 99999999.99");
                    table.CheckConstraint("ck_order_items_product_name_length", "char_length(product_name) BETWEEN 1 AND 96");
                    table.CheckConstraint("ck_order_items_quantity_range", "quantity BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_order_items_size_name_length", "char_length(size_name) BETWEEN 1 AND 32");
                    table.CheckConstraint("ck_order_items_unit_price_range", "unit_price >= 0 AND unit_price <= 99999999.99");
                    table.ForeignKey(
                        name: "fk_order_items_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_items_order_id",
                        column: x => x.order_id,
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_items_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_items_product_size_id",
                        column: x => x.product_size_id,
                        principalTable: "product_sizes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_items_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_items_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_item_addons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_addon_id = table.Column<Guid>(type: "uuid", nullable: true),
                    addon_name = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_item_addons", x => x.id);
                    table.CheckConstraint("ck_order_item_addons_name_length", "char_length(addon_name) BETWEEN 1 AND 48");
                    table.CheckConstraint("ck_order_item_addons_price_range", "price >= 0 AND price <= 99999999.99");
                    table.ForeignKey(
                        name: "fk_order_item_addons_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_item_addons_order_item_id",
                        column: x => x.order_item_id,
                        principalTable: "order_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_item_addons_product_addon_id",
                        column: x => x.product_addon_id,
                        principalTable: "product_addons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_order_item_addons_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_order_item_addons_created_by",
                table: "order_item_addons",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_addons_order_item_id",
                table: "order_item_addons",
                column: "order_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_addons_product_addon_id",
                table: "order_item_addons",
                column: "product_addon_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_item_addons_updated_by",
                table: "order_item_addons",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_created_by",
                table: "order_items",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id",
                table: "order_items",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_product_id",
                table: "order_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_product_size_id",
                table: "order_items",
                column: "product_size_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_station_id",
                table: "order_items",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_updated_by",
                table: "order_items",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_order_items_order_id_line_number",
                table: "order_items",
                columns: new[] { "order_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_branch_id_status_submitted_at",
                table: "orders",
                columns: new[] { "branch_id", "status", "submitted_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_by",
                table: "orders",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_orders_placed_by_user_id",
                table: "orders",
                column: "placed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_responded_by_user_id",
                table: "orders",
                column: "responded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_table_id",
                table: "orders",
                column: "table_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_updated_by",
                table: "orders",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "uq_orders_branch_business_date_number",
                table: "orders",
                columns: new[] { "branch_id", "business_date", "order_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_orders_branch_idempotency_key",
                table: "orders",
                columns: new[] { "branch_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_orders_public_reference",
                table: "orders",
                column: "public_reference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "order_item_addons");

            migrationBuilder.DropTable(
                name: "order_number_counters");

            migrationBuilder.DropTable(
                name: "order_items");

            migrationBuilder.DropTable(
                name: "orders");
        }
    }
}
