using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CafeHub.Infrastructure.Persistence.Tenant.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "menu_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("pk_menu_categories", x => x.id);
                    table.CheckConstraint("ck_menu_categories_name_length", "char_length(name) BETWEEN 1 AND 64");
                    table.CheckConstraint("ck_menu_categories_parent_not_self", "parent_id <> id");
                    table.CheckConstraint("ck_menu_categories_sort_order", "sort_order >= 0");
                    table.CheckConstraint("ck_menu_categories_status", "status IN ('Active', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_menu_categories_created_by",
                        column: x => x.created_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_menu_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "menu_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_menu_categories_updated_by",
                        column: x => x.updated_by,
                        principalTable: "tenant_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_menu_categories_created_by",
                table: "menu_categories",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_menu_categories_parent_id_sort_order_id",
                table: "menu_categories",
                columns: new[] { "parent_id", "sort_order", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_menu_categories_updated_by",
                table: "menu_categories",
                column: "updated_by");

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX uq_menu_categories_active_root_name
                ON menu_categories (lower(name))
                WHERE parent_id IS NULL AND status = 'Active';

                CREATE UNIQUE INDEX uq_menu_categories_active_child_name
                ON menu_categories (parent_id, lower(name))
                WHERE parent_id IS NOT NULL AND status = 'Active';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS uq_menu_categories_active_child_name;
                DROP INDEX IF EXISTS uq_menu_categories_active_root_name;
                """);

            migrationBuilder.DropTable(
                name: "menu_categories");
        }
    }
}
