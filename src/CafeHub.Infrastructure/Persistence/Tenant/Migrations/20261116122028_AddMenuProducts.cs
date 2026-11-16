using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "currency_code",
                table: "cafe_settings",
                type: "text",
                nullable: false,
                defaultValue: "egp");

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    station_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    ingredients = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    caffeine = table.Column<bool>(type: "boolean", nullable: false),
                    preparation_minutes = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_description_length", "description IS NULL OR (length(description) >= 1 AND length(description) <= 1000)");
                    table.CheckConstraint("ck_products_ingredients_length", "ingredients IS NULL OR (length(ingredients) >= 1 AND length(ingredients) <= 1000)");
                    table.CheckConstraint("ck_products_kind", "kind IN ('Food', 'Drink')");
                    table.CheckConstraint("ck_products_name_length", "char_length(name) BETWEEN 1 AND 96");
                    table.CheckConstraint("ck_products_preparation_minutes", "preparation_minutes IS NULL OR (preparation_minutes >= 0 AND preparation_minutes <= 240)");
                    table.CheckConstraint("ck_products_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_products_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_products_category_id",
                        column: x => x.category_id,
                        principalTable: "menu_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_station_id",
                        column: x => x.station_id,
                        principalTable: "stations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_addons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_addons", x => x.id);
                    table.CheckConstraint("ck_product_addons_name_length", "char_length(name) BETWEEN 1 AND 48");
                    table.CheckConstraint("ck_product_addons_price_range", "price >= 0 AND price <= 99999999.99");
                    table.CheckConstraint("ck_product_addons_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_product_addons_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_addons_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_addons_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_sizes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_sizes", x => x.id);
                    table.CheckConstraint("ck_product_sizes_name_length", "char_length(name) BETWEEN 1 AND 32");
                    table.CheckConstraint("ck_product_sizes_price_range", "price >= 0 AND price <= 99999999.99");
                    table.CheckConstraint("ck_product_sizes_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_product_sizes_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_sizes_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_sizes_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_tags",
                columns: table => new
                {
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_tags", x => new { x.product_id, x.tag });
                    table.CheckConstraint("ck_product_tags_tag_format", "tag ~ '^[a-z0-9 -]+$'");
                    table.CheckConstraint("ck_product_tags_tag_length", "char_length(tag) BETWEEN 1 AND 32");
                    table.CheckConstraint("ck_product_tags_tag_lowercase", "tag = lower(tag)");
                    table.ForeignKey(
                        name: "fk_product_tags_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_cafe_settings_currency_code_format",
                table: "cafe_settings",
                sql: "currency_code ~ '^[a-z]{3}$'");

            migrationBuilder.CreateIndex(
                name: "ix_product_addons_created_by",
                table: "product_addons",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_product_addons_product_id",
                table: "product_addons",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_addons_updated_by",
                table: "product_addons",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_created_by",
                table: "product_sizes",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_product_id",
                table: "product_sizes",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_sizes_updated_by",
                table: "product_sizes",
                column: "updated_by");

            migrationBuilder.CreateIndex(
                name: "ix_product_tags_tag",
                table: "product_tags",
                column: "tag");

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_created_by",
                table: "products",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_products_station_id",
                table: "products",
                column: "station_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_updated_by",
                table: "products",
                column: "updated_by");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_product_sizes_active_default
                ON product_sizes (product_id)
                WHERE is_default AND status = 'Active';

                CREATE UNIQUE INDEX uq_product_sizes_active_name
                ON product_sizes (product_id, lower(name))
                WHERE status = 'Active';

                CREATE UNIQUE INDEX uq_product_addons_active_name
                ON product_addons (product_id, lower(name))
                WHERE status = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_product_addons_active_name;
                DROP INDEX IF EXISTS uq_product_sizes_active_name;
                DROP INDEX IF EXISTS uq_product_sizes_active_default;
                """);

            migrationBuilder.DropTable(
                name: "product_addons");

            migrationBuilder.DropTable(
                name: "product_sizes");

            migrationBuilder.DropTable(
                name: "product_tags");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_cafe_settings_currency_code_format",
                table: "cafe_settings");

            migrationBuilder.DropColumn(
                name: "currency_code",
                table: "cafe_settings");
        }
    }
}
